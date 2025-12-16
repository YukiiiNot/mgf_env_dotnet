namespace MGF.Tools.SquareImport.Importers;

using System.Diagnostics.CodeAnalysis;
using MGF.Tools.SquareImport.Normalization;
using MGF.Tools.SquareImport.Parsing;

internal sealed partial class CustomersImporter
{
    private sealed record CustomerIdentity(
        SquareCustomerRow Row,
        string SquareCustomerId,
        string DisplayName,
        IReadOnlyList<string> NormalizedEmails,
        IReadOnlyList<string> NormalizedPhones,
        string? PersonNameKey,
        string? OrganizationNameKey,
        string? CityKey,
        string? StateKey,
        string? PostalCodeKey,
        string ProposedClientTypeKey,
        string ClassificationReason,
        bool ShouldCreatePerson
    )
    {
        public string? PrimaryNormalizedEmail => NormalizedEmails.Count == 0 ? null : NormalizedEmails[0];
        public string? PrimaryNormalizedPhone => NormalizedPhones.Count == 0 ? null : NormalizedPhones[0];
        public string SourceFileName => Path.GetFileName(Row.SourceFile);
    }

    private sealed record HardDuplicateRowInfo(
        bool IsHardDuplicate,
        bool IsPrimary,
        string PrimarySquareCustomerId,
        IReadOnlyList<string> DuplicateEmails,
        IReadOnlyList<string> DuplicatePhones
    );

    private sealed class HardDuplicates
    {
        public HardDuplicates(
            int rowCount,
            IReadOnlyDictionary<string, HardDuplicateRowInfo> bySquareCustomerId,
            IReadOnlyList<(string Email, IReadOnlyList<string> SquareCustomerIds)> duplicateEmails,
            IReadOnlyList<(string Phone, IReadOnlyList<string> SquareCustomerIds)> duplicatePhones
        )
        {
            RowCount = rowCount;
            BySquareCustomerId = bySquareCustomerId;
            DuplicateEmails = duplicateEmails;
            DuplicatePhones = duplicatePhones;
        }

        public int RowCount { get; }
        public IReadOnlyDictionary<string, HardDuplicateRowInfo> BySquareCustomerId { get; }
        public IReadOnlyList<(string Email, IReadOnlyList<string> SquareCustomerIds)> DuplicateEmails { get; }
        public IReadOnlyList<(string Phone, IReadOnlyList<string> SquareCustomerIds)> DuplicatePhones { get; }
    }

    private sealed record SoftDuplicateRowInfo(IReadOnlyList<string> Reasons);

    private sealed class SoftDuplicates
    {
        public SoftDuplicates(int rowCount, IReadOnlyDictionary<string, SoftDuplicateRowInfo> bySquareCustomerId)
        {
            RowCount = rowCount;
            BySquareCustomerId = bySquareCustomerId;
        }

        public int RowCount { get; }
        public IReadOnlyDictionary<string, SoftDuplicateRowInfo> BySquareCustomerId { get; }
    }

    private static CustomerIdentity BuildCustomerIdentity(SquareCustomerRow row)
    {
        var squareCustomerId = row.SquareCustomerId?.Trim() ?? string.Empty;
        var displayName = ComputeClientDisplayName(row, squareCustomerId);

        var (clientTypeKey, classificationReason) = ClassifyClientType(row, displayName);

        var normalizedEmails = NormalizeMultiValue(row.EmailAddress, IdentityKeys.NormalizeEmail);
        var normalizedPhones = NormalizeMultiValue(row.PhoneNumber, IdentityKeys.NormalizePhone);

        var personNameKey = BuildPersonNameKey(row, displayName);
        var organizationNameKey = BuildOrganizationNameKey(row, displayName, clientTypeKey);

        var cityKey = NormalizeKeyLower(row.City);
        var stateKey = NormalizeStateKey(row.State);
        var postalKey = NormalizePostalKey(row.PostalCode);

        var shouldCreatePerson = !string.IsNullOrWhiteSpace(row.FirstName) || !string.IsNullOrWhiteSpace(row.LastName);
        if (!string.Equals(clientTypeKey, "organization", StringComparison.Ordinal))
        {
            shouldCreatePerson = shouldCreatePerson || normalizedEmails.Count > 0 || normalizedPhones.Count > 0;
        }

        return new CustomerIdentity(
            Row: row,
            SquareCustomerId: squareCustomerId,
            DisplayName: displayName,
            NormalizedEmails: normalizedEmails,
            NormalizedPhones: normalizedPhones,
            PersonNameKey: personNameKey,
            OrganizationNameKey: organizationNameKey,
            CityKey: cityKey,
            StateKey: stateKey,
            PostalCodeKey: postalKey,
            ProposedClientTypeKey: clientTypeKey,
            ClassificationReason: classificationReason,
            ShouldCreatePerson: shouldCreatePerson
        );
    }

    private static HardDuplicates DetectHardDuplicates(IReadOnlyList<CustomerIdentity> inputs)
    {
        var duplicateEmailsById = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var duplicatePhonesById = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        var emailToSquareIds = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var phoneToSquareIds = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        var squareIdToRowNumber = inputs.ToDictionary(x => x.SquareCustomerId, x => x.Row.RowNumber, StringComparer.Ordinal);

        foreach (var identity in inputs)
        {
            foreach (var email in identity.NormalizedEmails)
            {
                if (!emailToSquareIds.TryGetValue(email, out var ids))
                {
                    ids = new HashSet<string>(StringComparer.Ordinal);
                    emailToSquareIds[email] = ids;
                }

                ids.Add(identity.SquareCustomerId);
            }

            foreach (var phone in identity.NormalizedPhones)
            {
                if (!phoneToSquareIds.TryGetValue(phone, out var ids))
                {
                    ids = new HashSet<string>(StringComparer.Ordinal);
                    phoneToSquareIds[phone] = ids;
                }

                ids.Add(identity.SquareCustomerId);
            }
        }

        var duplicateEmails = emailToSquareIds
            .Where(kvp => kvp.Value.Count > 1)
            .Select(
                kvp =>
                    (
                        Email: kvp.Key,
                        SquareCustomerIds: (IReadOnlyList<string>)kvp.Value.OrderBy(x => x, StringComparer.Ordinal).ToList()
                    )
            )
            .OrderByDescending(x => x.SquareCustomerIds.Count)
            .ThenBy(x => x.Email, StringComparer.Ordinal)
            .ToList();

        var duplicatePhones = phoneToSquareIds
            .Where(kvp => kvp.Value.Count > 1)
            .Select(
                kvp =>
                    (
                        Phone: kvp.Key,
                        SquareCustomerIds: (IReadOnlyList<string>)kvp.Value.OrderBy(x => x, StringComparer.Ordinal).ToList()
                    )
            )
            .OrderByDescending(x => x.SquareCustomerIds.Count)
            .ThenBy(x => x.Phone, StringComparer.Ordinal)
            .ToList();

        foreach (var group in duplicateEmails)
        {
            foreach (var squareCustomerId in group.SquareCustomerIds)
            {
                if (!duplicateEmailsById.TryGetValue(squareCustomerId, out var emails))
                {
                    emails = new HashSet<string>(StringComparer.Ordinal);
                    duplicateEmailsById[squareCustomerId] = emails;
                }

                emails.Add(group.Email);
            }
        }

        foreach (var group in duplicatePhones)
        {
            foreach (var squareCustomerId in group.SquareCustomerIds)
            {
                if (!duplicatePhonesById.TryGetValue(squareCustomerId, out var phones))
                {
                    phones = new HashSet<string>(StringComparer.Ordinal);
                    duplicatePhonesById[squareCustomerId] = phones;
                }

                phones.Add(group.Phone);
            }
        }

        var neighbors = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        static void AddNeighbor(Dictionary<string, HashSet<string>> neighbors, string a, string b)
        {
            if (!neighbors.TryGetValue(a, out var set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                neighbors[a] = set;
            }

            set.Add(b);
        }

        static void ConnectGroup(Dictionary<string, HashSet<string>> neighbors, IReadOnlyList<string> ids)
        {
            if (ids.Count < 2)
            {
                return;
            }

            var first = ids[0];
            for (var i = 1; i < ids.Count; i++)
            {
                var other = ids[i];
                AddNeighbor(neighbors, first, other);
                AddNeighbor(neighbors, other, first);
            }
        }

        foreach (var group in duplicateEmails)
        {
            ConnectGroup(neighbors, group.SquareCustomerIds);
        }

        foreach (var group in duplicatePhones)
        {
            ConnectGroup(neighbors, group.SquareCustomerIds);
        }

        var componentPrimaryById = new Dictionary<string, string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);

        foreach (var startId in neighbors.Keys.OrderBy(x => x, StringComparer.Ordinal))
        {
            if (!visited.Add(startId))
            {
                continue;
            }

            var stack = new Stack<string>();
            stack.Push(startId);

            var component = new List<string>();
            while (stack.Count > 0)
            {
                var id = stack.Pop();
                component.Add(id);

                if (!neighbors.TryGetValue(id, out var next))
                {
                    continue;
                }

                foreach (var candidate in next)
                {
                    if (visited.Add(candidate))
                    {
                        stack.Push(candidate);
                    }
                }
            }

            var primaryId = component
                .OrderBy(id => squareIdToRowNumber.TryGetValue(id, out var rn) ? rn : int.MaxValue)
                .ThenBy(id => id, StringComparer.Ordinal)
                .First();

            foreach (var id in component)
            {
                componentPrimaryById[id] = primaryId;
            }
        }

        var allHardDupeIds = duplicateEmailsById.Keys
            .Concat(duplicatePhonesById.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        var bySquareCustomerId = new Dictionary<string, HardDuplicateRowInfo>(StringComparer.Ordinal);
        foreach (var squareCustomerId in allHardDupeIds)
        {
            var primaryId = componentPrimaryById.TryGetValue(squareCustomerId, out var p) ? p : squareCustomerId;
            bySquareCustomerId[squareCustomerId] = new HardDuplicateRowInfo(
                IsHardDuplicate: true,
                IsPrimary: squareCustomerId == primaryId,
                PrimarySquareCustomerId: primaryId,
                DuplicateEmails: duplicateEmailsById.TryGetValue(squareCustomerId, out var emails)
                    ? emails.OrderBy(x => x, StringComparer.Ordinal).ToList()
                    : Array.Empty<string>(),
                DuplicatePhones: duplicatePhonesById.TryGetValue(squareCustomerId, out var phones)
                    ? phones.OrderBy(x => x, StringComparer.Ordinal).ToList()
                    : Array.Empty<string>()
            );
        }

        return new HardDuplicates(
            rowCount: allHardDupeIds.Count,
            bySquareCustomerId: bySquareCustomerId,
            duplicateEmails: duplicateEmails,
            duplicatePhones: duplicatePhones
        );
    }

    private static SoftDuplicates DetectSoftDuplicates(IReadOnlyList<CustomerIdentity> inputs)
    {
        var reasonsById = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        static void AddReason(Dictionary<string, HashSet<string>> reasonsById, IReadOnlyList<string> ids, string reasonPrefix)
        {
            if (ids.Count < 2)
            {
                return;
            }

            foreach (var id in ids)
            {
                if (!reasonsById.TryGetValue(id, out var reasons))
                {
                    reasons = new HashSet<string>(StringComparer.Ordinal);
                    reasonsById[id] = reasons;
                }

                reasons.Add(reasonPrefix);
            }
        }

        // Soft duplicate rule 1: same person name + same postal OR same city+state.
        var nameAndLocationGroups = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var identity in inputs)
        {
            if (!string.Equals(identity.ProposedClientTypeKey, "individual", StringComparison.Ordinal))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(identity.PersonNameKey))
            {
                continue;
            }

            string? key = null;
            if (!string.IsNullOrWhiteSpace(identity.PostalCodeKey))
            {
                key = $"name_postal:{identity.PersonNameKey}|{identity.PostalCodeKey}";
            }
            else if (!string.IsNullOrWhiteSpace(identity.CityKey) && !string.IsNullOrWhiteSpace(identity.StateKey))
            {
                key = $"name_city_state:{identity.PersonNameKey}|{identity.CityKey}|{identity.StateKey}";
            }

            if (key is null)
            {
                continue;
            }

            if (!nameAndLocationGroups.TryGetValue(key, out var ids))
            {
                ids = new List<string>();
                nameAndLocationGroups[key] = ids;
            }

            ids.Add(identity.SquareCustomerId);
        }

        foreach (var kvp in nameAndLocationGroups.Where(k => k.Value.Distinct(StringComparer.Ordinal).Count() > 1))
        {
            var ids = kvp.Value.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();
            AddReason(reasonsById, ids, kvp.Key);
        }

        // Soft duplicate rule 2: similar display names by token signature.
        var displayKeyGroups = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var identity in inputs)
        {
            var key = ComputeDisplaySimilarityKey(identity.DisplayName);
            if (key is null)
            {
                continue;
            }

            if (!displayKeyGroups.TryGetValue(key, out var ids))
            {
                ids = new List<string>();
                displayKeyGroups[key] = ids;
            }

            ids.Add(identity.SquareCustomerId);
        }

        foreach (var kvp in displayKeyGroups.Where(k => k.Value.Distinct(StringComparer.Ordinal).Count() > 1))
        {
            var ids = kvp.Value.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();
            AddReason(reasonsById, ids, $"display_tokens:{kvp.Key}");
        }

        var bySquareCustomerId = reasonsById.ToDictionary(
            kvp => kvp.Key,
            kvp => new SoftDuplicateRowInfo(kvp.Value.OrderBy(x => x, StringComparer.Ordinal).ToList()),
            StringComparer.Ordinal
        );

        return new SoftDuplicates(rowCount: bySquareCustomerId.Count, bySquareCustomerId: bySquareCustomerId);
    }

    private static IReadOnlyList<string> NormalizeMultiValue(string? value, Func<string?, string?> normalizer)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        var parts = value.Split(',', ';', '\n', '\r');
        var results = new List<string>(capacity: parts.Length);

        foreach (var part in parts)
        {
            var normalized = normalizer(part);
            if (normalized is not null && !results.Contains(normalized, StringComparer.Ordinal))
            {
                results.Add(normalized);
            }
        }

        return results;
    }

    private static string? BuildPersonNameKey(SquareCustomerRow row, string displayName)
    {
        var first = IdentityKeys.NormalizeName(row.FirstName);
        var last = IdentityKeys.NormalizeName(row.LastName);

        if (!string.IsNullOrWhiteSpace(first) || !string.IsNullOrWhiteSpace(last))
        {
            return NormalizeKeyLower($"{first} {last}".Trim());
        }

        return NormalizeKeyLower(displayName);
    }

    private static string? BuildOrganizationNameKey(SquareCustomerRow row, string displayName, string proposedClientTypeKey)
    {
        var company = IdentityKeys.NormalizeName(row.CompanyName);
        if (!string.IsNullOrWhiteSpace(company))
        {
            return NormalizeKeyLower(company);
        }

        if (string.Equals(proposedClientTypeKey, "organization", StringComparison.Ordinal))
        {
            return NormalizeKeyLower(displayName);
        }

        return null;
    }

    private static (string ClientTypeKey, string Reason) ClassifyClientType(SquareCustomerRow row, string displayName)
    {
        if (!string.IsNullOrWhiteSpace(row.CompanyName))
        {
            return ("organization", "company_name_present");
        }

        if (!string.IsNullOrWhiteSpace(row.FirstName) || !string.IsNullOrWhiteSpace(row.LastName))
        {
            return ("individual", "person_name_present");
        }

        if (ContainsOrganizationIndicator(displayName, out var indicator))
        {
            return ("organization", $"display_name_indicator:{indicator}");
        }

        return ("individual", "default");
    }

    private static bool ContainsOrganizationIndicator(string displayName, [NotNullWhen(true)] out string? indicator)
    {
        indicator = null;

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return false;
        }

        var tokens = TokenizeUpper(displayName);
        foreach (var token in tokens)
        {
            if (OrganizationIndicatorTokens.Contains(token, StringComparer.Ordinal))
            {
                indicator = token;
                return true;
            }
        }

        return false;
    }

    private static readonly string[] OrganizationIndicatorTokens =
    {
        // Sample expectations (deterministic):
        // - CompanyName="MGF LLC" => organization (company_name_present)
        // - DisplayName="Kappa Alpha Theta Chapter" => organization (display_name_indicator:CHAPTER)
        // - DisplayName="Acme Studios" and "Acme Studio" => display token key "acme studio" (soft dup rule)
        "LLC",
        "INC",
        "CO",
        "COMPANY",
        "STUDIO",
        "SCHOOL",
        "CHURCH",
        "FOUNDATION",
        "SORORITY",
        "FRATERNITY",
        "CHAPTER",
        "UNIVERSITY",
        "COLLEGE",
        "DEPARTMENT",
        "ASSOCIATION",
        "CLUB",
    };

    private static IReadOnlyList<string> TokenizeUpper(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        var tokens = new List<string>();
        var current = new List<char>();

        foreach (var c in value)
        {
            if (char.IsLetterOrDigit(c))
            {
                current.Add(char.ToUpperInvariant(c));
                continue;
            }

            if (current.Count > 0)
            {
                tokens.Add(new string(current.ToArray()));
                current.Clear();
            }
        }

        if (current.Count > 0)
        {
            tokens.Add(new string(current.ToArray()));
        }

        return tokens;
    }

    private static string? NormalizeKeyLower(string? value)
    {
        var normalized = IdentityKeys.NormalizeName(value);
        return normalized?.ToLowerInvariant();
    }

    private static string? NormalizeStateKey(string? value)
    {
        var normalized = IdentityKeys.NormalizeName(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Trim().ToUpperInvariant();
    }

    private static string? NormalizePostalKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        Span<char> buffer = stackalloc char[value.Length];
        var idx = 0;
        foreach (var c in value)
        {
            if (char.IsLetterOrDigit(c))
            {
                buffer[idx++] = char.ToUpperInvariant(c);
            }
        }

        if (idx == 0)
        {
            return null;
        }

        return new string(buffer[..idx]);
    }

    private static string? ComputeDisplaySimilarityKey(string displayName)
    {
        var normalized = NormalizeKeyLower(displayName);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var tokens = new HashSet<string>(StringComparer.Ordinal);
        var current = new List<char>();

        foreach (var c in normalized)
        {
            if (char.IsLetterOrDigit(c))
            {
                current.Add(c);
                continue;
            }

            AddToken(tokens, current);
        }

        AddToken(tokens, current);

        if (tokens.Count < 2)
        {
            return null;
        }

        var sorted = tokens.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        return string.Join(' ', sorted);

        static void AddToken(HashSet<string> tokens, List<char> current)
        {
            if (current.Count == 0)
            {
                return;
            }

            var token = new string(current.ToArray());
            current.Clear();

            if (token.Length <= 1)
            {
                return;
            }

            if (OrganizationIndicatorTokens.Contains(token.ToUpperInvariant(), StringComparer.Ordinal))
            {
                return;
            }

            if (token.EndsWith('s') && token.Length > 3)
            {
                token = token[..^1];
            }

            tokens.Add(token);
        }
    }
}
