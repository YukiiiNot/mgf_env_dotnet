using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using MGF.Tools.LegacyAudit.Commands;

var root = RootCommandFactory.Create();
var parser = new CommandLineBuilder(root).UseDefaults().Build();
return await parser.InvokeAsync(args);
