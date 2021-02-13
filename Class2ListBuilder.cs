using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ClassToListBuilder
{
    [Generator]
    public class Generator : ISourceGenerator
    {
        List<string> variableList = new List<string>();

        Dictionary<string, List<ClassDeclarationSyntax>> tracker = new Dictionary<string, List<ClassDeclarationSyntax>>();

        public void Execute(GeneratorExecutionContext context)
        {
            Compilation compilation = context.Compilation;

            // Retreive the populated receiver 
            if (!(context.SyntaxReceiver is SyntaxReceiver receiver))
                return;

            // Group all classes with the same namespace together
            foreach (ClassDeclarationSyntax property in receiver.CandidateFields)
            {
                SemanticModel model = compilation.GetSemanticModel(property.SyntaxTree);

                SyntaxNode namespaceNode = property.Parent;
                string @namespace = ((NamespaceDeclarationSyntax)namespaceNode).Name.ToString();

                if (tracker.ContainsKey(@namespace))
                {
                    tracker[@namespace].Add(property);
                }
                else
                {
                    tracker.Add(@namespace, new List<ClassDeclarationSyntax>() { property });
                }
            }

            // Loop through all the keys
            foreach (var item in tracker.Keys)
            {
                variableList.Clear();
                string currentNamespace = item;

                StringBuilder sbNoArg = new StringBuilder();

                sbNoArg.Append($@"
using System;
using System.Collections.Generic;

namespace {currentNamespace}
{{
    public static partial class GeneratedList
    {{            ");

                sbNoArg.Append(Environment.NewLine);

                foreach (ClassDeclarationSyntax cds in tracker[item])
                {
                    variableList.Clear();
                    SemanticModel model = compilation.GetSemanticModel(cds.SyntaxTree);

                    var list = cds.DescendantNodes().OfType<FieldDeclarationSyntax>().ToList();

                    foreach (var fds in list)
                    {
                        foreach (var record in fds.Declaration.Variables)
                        {
                            var constantValue = model.GetConstantValue(record.Initializer.Value);
                            variableList.Add(constantValue.Value.ToString());
                        }
                    }

                    sbNoArg.AppendLine($"public static readonly List<string> {cds.Identifier.ToFullString()} = new List<string>(){{ {string.Join(",", variableList.ToArray())} }};");
                }

                sbNoArg.Append($@"
    }}
}}");

                context.AddSource($"{currentNamespace}.cs", SourceText.From(sbNoArg.ToString(), Encoding.UTF8));
            }
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            // Register a syntax receiver that will be created for each generation pass.
            // In this case, to detect specific attribute param.
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        /// <summary>
        /// Created on demand before each generation pass.
        /// </summary>
        class SyntaxReceiver : ISyntaxReceiver
        {
            public List<ClassDeclarationSyntax> CandidateFields { get; } = new List<ClassDeclarationSyntax>();

            /// <summary>
            /// Called for every syntax node in the compilation, we can inspect the nodes and save any information useful for generation.
            /// </summary>
            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                // Only handle field syntax with a single attribute
                if (syntaxNode is ClassDeclarationSyntax classDeclarationSyntax
                    && classDeclarationSyntax.AttributeLists.Count == 1)
                {
                    // Extract attribute name
                    var firstAttribute = classDeclarationSyntax.AttributeLists.First().Attributes.First();
                    var attributeName = firstAttribute.Name.NormalizeWhitespace().ToFullString();

                    // Process only those with the pre-defined attribute
                    if (attributeName.Equals("ClassToList"))
                        CandidateFields.Add(classDeclarationSyntax);
                }
            }
        }
    }
}
