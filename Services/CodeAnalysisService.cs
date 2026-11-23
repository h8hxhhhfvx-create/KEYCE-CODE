
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AppDiagnostic = KeyceCode.Models.Diagnostic;

namespace KeyceCode.Services
{
    public class CodeAnalysisService
    {
        private static readonly Lazy<IReadOnlyList<MetadataReference>> PlatformReferences = new(CreatePlatformReferences);

        public List<AppDiagnostic> Analyze(string sourceCode)
        {
            List<AppDiagnostic> errors = new List<AppDiagnostic>();

            // 1. Lire le code C#
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

            // 2. Créer une compilation temporaire avec les références mises en cache.
            CSharpCompilation compilation = CSharpCompilation.Create(
                "CodeAnalysis",
                syntaxTrees: new[] { syntaxTree },
                references: PlatformReferences.Value,
                options: new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary
                )
            );

            // 4. Récupérer les erreurs et avertissements
            var diagnostics = compilation.GetDiagnostics();

            foreach (var diagnostic in diagnostics)
            {
                if (diagnostic.Severity != DiagnosticSeverity.Error &&
                    diagnostic.Severity != DiagnosticSeverity.Warning)
                {
                    continue;
                }

                if (!diagnostic.Location.IsInSource)
                    continue;

                // 5. Récupérer la position du problème
                var position = diagnostic.Location.GetLineSpan();

                int line = position.StartLinePosition.Line + 1;
                int column = position.StartLinePosition.Character + 1;

                int endLine = position.EndLinePosition.Line + 1;
                int endColumn = position.EndLinePosition.Character + 1;

                // 6. Créer notre objet Diagnostic
                AppDiagnostic error = new AppDiagnostic
                {
                    Code = diagnostic.Id,
                    Message = diagnostic.GetMessage(),
                    Severity = diagnostic.Severity.ToString(),
                    Line = line,
                    Column = column,
                    EndLine = endLine,
                    EndColumn = endColumn
                };

                errors.Add(error);
            }

            return errors;
        }

        private static IReadOnlyList<MetadataReference> CreatePlatformReferences()
        {
            string? trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
            if (string.IsNullOrEmpty(trustedAssemblies))
                throw new InvalidOperationException("Impossible de récupérer les références .NET.");

            return trustedAssemblies
                .Split(Path.PathSeparator)
                .Where(File.Exists)
                .Select(path => MetadataReference.CreateFromFile(path))
                .ToArray();
        }
    }
}
