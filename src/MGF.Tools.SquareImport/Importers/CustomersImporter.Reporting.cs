namespace MGF.Tools.SquareImport.Importers;

internal sealed partial class CustomersImporter
{
    private static void WriteDetailedSummary(
        CustomersImportStats stats,
        HardDuplicates hardDuplicates,
        SoftDuplicates softDuplicates,
        string reportDirPath,
        bool reportsWritten
    )
    {
        Console.WriteLine(
            $"square-import customers: rows total={stats.TotalRows} inserted={stats.RowsInserted} updated={stats.RowsUpdated} linked={stats.RowsLinked} skipped={stats.RowsSkipped} needs_review={stats.RowsNeedsReview} errors={stats.Errors}"
        );

        Console.WriteLine(
            $"square-import customers: classification organizations={stats.ClassifiedOrganizations} individuals={stats.ClassifiedIndividuals}"
        );

        Console.WriteLine(
            $"square-import customers: duplicates hard_rows={hardDuplicates.RowCount} soft_rows={softDuplicates.RowCount} duplicate_square_customer_ids_skipped={stats.DuplicateSquareCustomerIdsSkipped}"
        );

        Console.WriteLine(
            $"square-import customers: auto_linked_by_email={stats.AutoLinkedByEmail} auto_linked_by_phone={stats.AutoLinkedByPhone}"
        );

        Console.WriteLine(
            $"square-import customers: clients inserted={stats.ClientsInserted} updated={stats.ClientsUpdated} skipped={stats.ClientsSkipped}"
        );
        Console.WriteLine(
            $"square-import customers: people inserted={stats.PeopleInserted} updated={stats.PeopleUpdated} skipped={stats.PeopleSkipped}"
        );
        Console.WriteLine(
            $"square-import customers: person_contacts inserted={stats.PersonContactsInserted} updated={stats.PersonContactsUpdated} skipped={stats.PersonContactsSkipped}"
        );
        Console.WriteLine(
            $"square-import customers: client_contacts inserted={stats.ClientContactsInserted} updated={stats.ClientContactsUpdated} skipped={stats.ClientContactsSkipped}"
        );
        Console.WriteLine(
            $"square-import customers: client_integrations_square inserted={stats.ClientIntegrationsInserted} updated={stats.ClientIntegrationsUpdated} skipped={stats.ClientIntegrationsSkipped}"
        );

        if (reportsWritten)
        {
            Console.WriteLine($"square-import customers: reports dir={reportDirPath}");
        }

        WriteTopHardDuplicates("email", hardDuplicates.DuplicateEmails.Select(x => (Key: x.Email, x.SquareCustomerIds)));
        WriteTopHardDuplicates("phone", hardDuplicates.DuplicatePhones.Select(x => (Key: x.Phone, x.SquareCustomerIds)));
    }

    private static void WriteTopHardDuplicates(
        string kind,
        IEnumerable<(string Key, IReadOnlyList<string> SquareCustomerIds)> groups
    )
    {
        var top = groups
            .Where(x => x.SquareCustomerIds.Count > 1)
            .OrderByDescending(x => x.SquareCustomerIds.Count)
            .ThenBy(x => x.Key, StringComparer.Ordinal)
            .Take(20)
            .ToList();

        if (top.Count == 0)
        {
            Console.WriteLine($"square-import customers: hard_duplicate_{kind}_top20=none");
            return;
        }

        Console.WriteLine($"square-import customers: hard_duplicate_{kind}_top20={top.Count}");
        foreach (var item in top)
        {
            var ids = item.SquareCustomerIds.Count <= 10
                ? string.Join(", ", item.SquareCustomerIds)
                : string.Join(", ", item.SquareCustomerIds.Take(10)) + ", ...";
            Console.WriteLine($"square-import customers: hard_dupe_{kind} {item.Key} count={item.SquareCustomerIds.Count} => {ids}");
        }
    }
}

