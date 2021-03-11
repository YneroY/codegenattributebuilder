using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EnumInterpreterBuilder
{
    [Generator]
    public class SourceGenerator : ISourceGenerator
    {
        /// <summary>
        /// Storing enum data to be used for class construction.
        /// </summary>
        public Dictionary<string, Dictionary<string, string>> enumData = new Dictionary<string, Dictionary<string, string>>();

        string definedNs = "EnumToConstants";

        public void Execute(GeneratorExecutionContext context)
        {
            Compilation compilation = context.Compilation;

            // Retreive the populated receiver 
            if (!(context.SyntaxReceiver is SyntaxReceiver receiver))
                return;

            // Group all classes with the same namespace together
            foreach (EnumDeclarationSyntax @enum in receiver.CandidateFields)
            {
                string className = @enum.Identifier.ToFullString();

                // enum identifier is the key/class name
                if (enumData.ContainsKey(className))
                // If the enum type has been processed before, just skip
                    continue;

                enumData.Add(className, new Dictionary<string, string>());

                // List all items in an enum
                var list = @enum.Members;

                foreach (var emds in list)
                // Looping through all the enum items
                {
                    SemanticModel model = compilation.GetSemanticModel(emds.SyntaxTree);

                    // XmlEnumAttribute/XmlEnum
                    var attributeList = emds.AttributeLists;

                    if (attributeList.Count() > 0)
                    {
                        bool isDesiredAttributeFound = false;

                        foreach (var attributeMember in emds.AttributeLists)
                        {
                            if (attributeMember.Attributes.First().ArgumentList.Arguments.Count() > 0)
                            // Ensure the attribute contains argument
                            {
                                var attributeName = model.GetTypeInfo(attributeMember.Attributes.First()).ConvertedType.Name;

                                if (attributeName == "XmlEnumAttribute" || attributeName == "XmlEnum")
                                {
                                    // Let's assume there can only be 1 argument
                                    enumData[className].Add(emds.Identifier.ToFullString(), 
                                                            attributeMember.Attributes.First().ArgumentList.Arguments.First().Expression.NormalizeWhitespace().ToFullString());

                                    isDesiredAttributeFound = true;
                                    break;
                                }
                            }
                        }

                        if(!isDesiredAttributeFound)
                        {
                            enumData[className].Add(emds.Identifier.ToFullString(), emds.Identifier.ToFullString());
                        }
                    }
                    else
                    {
                        enumData[className].Add(emds.Identifier.ToFullString(), emds.Identifier.ToFullString());
                    }
                }

                // Function to add into file to create the class
                StringBuilder classBuilder = new StringBuilder();

                classBuilder.AppendLine($@"
using System;
using System.Collections.Generic;

namespace {definedNs}
{{         ");

                foreach (var item in enumData)
                {
                    classBuilder.AppendLine($@"     public static class {item.Key}
                                                    {{  ");


                    foreach (var item2 in item.Value)
                    {
                        string myValue = item2.Value;

                        classBuilder.AppendLine($@"public const string {item2.Key} = {myValue};");
                        classBuilder.AppendLine("");
                    }

                    classBuilder.AppendLine($@"     }}");
                }

                classBuilder.AppendLine($@"     
}}");

                context.AddSource($"EnumToConstants.cs", SourceText.From(classBuilder.ToString(), Encoding.UTF8));
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
            public List<EnumDeclarationSyntax> CandidateFields { get; } = new List<EnumDeclarationSyntax>();

            /// <summary>
            /// Called for every syntax node in the compilation, we can inspect the nodes and save any information useful for generation.
            /// </summary>
            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                if (syntaxNode is EnumDeclarationSyntax enumDeclarationSyntax)                    
                {
                    CandidateFields.Add(enumDeclarationSyntax);
                }
            }
        }
    }

}
