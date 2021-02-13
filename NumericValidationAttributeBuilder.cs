using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace AttributeBuilder
{
    [Generator]
    public class Generator : ISourceGenerator
    {
        public const string VALUE_CHECKER = @"else
        {
            if (amount <= 0)
            {
                return new ValidationResult(string.Format(""{{0}} is invalid"", validationContext.DisplayName));
            }
        }";

        public const string VALUE_DECLARATION = @"public string? ZeroValueChecker { get; set; }";

        public void Execute(GeneratorExecutionContext context)
        {
            Compilation compilation = context.Compilation;

            // Retreive the populated receiver 
            if (!(context.SyntaxReceiver is SyntaxReceiver receiver))
                return;

            List<IFieldSymbol> fieldSymbols = new List<IFieldSymbol>();

            // To check if a specific validator has been generated or not
            HashSet<SpecialType> generatedValidator = new HashSet<SpecialType>();

            foreach (PropertyDeclarationSyntax property in receiver.CandidateFields)
            {
                SemanticModel model = compilation.GetSemanticModel(property.SyntaxTree);

                IPropertySymbol propertySymbol = model.GetDeclaredSymbol(property) as IPropertySymbol;

                // Property type
                ITypeSymbol fieldType = propertySymbol.Type;

                if((new List<SpecialType> { SpecialType.System_Decimal, SpecialType.System_Int32, SpecialType.System_Double, SpecialType.System_Int64 }).Contains(fieldType.SpecialType))
                // Only process if it's either decimal, double, int or long
                {
                    // Namespace
                    string compiledNamespace = propertySymbol.ContainingNamespace.ToDisplayString();

                    // Class name
                    string className = property.AttributeLists.First().Attributes.First().Name.NormalizeWhitespace().ToFullString();

                    // Read property to determine whether to include 0-value checker
                    // Here, we assume we only have a single attribute
                    var firstAttribute = property.AttributeLists.First().Attributes.First();

                    string elseStatement = "";
                    string zeroCheckerDeclaration = "";

                    if(firstAttribute.ArgumentList == null)
                    {
                        StringBuilder sbNoArg = new StringBuilder();


                        sbNoArg.Append($@"
using System;
using System.ComponentModel.DataAnnotations;

namespace {compiledNamespace}
{{
    [AttributeUsage(AttributeTargets.Property)]
    public class {className} : ValidationAttribute
    {{
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {{
            if (value == null)
                return new ValidationResult(string.Format(""{{0}} is required"", validationContext.DisplayName));

            if (string.IsNullOrEmpty(value.ToString()))
                            return new ValidationResult(string.Format(""{{0}} is required"", validationContext.DisplayName));

            {property.Type} amount = 0;

            if (!{property.Type}.TryParse(value.ToString(), out amount))
            {{
                return new ValidationResult(string.Format(""{{0}} is invalid"", validationContext.DisplayName));
            }}

            return ValidationResult.Success;
        }}
    }}
}}
            ");

                        context.AddSource($"{className}.cs", SourceText.From(sbNoArg.ToString(), Encoding.UTF8));

                        return;
                    }

                    if (firstAttribute.ArgumentList.Arguments.Count() > 0)
                    {
                        foreach (var item in firstAttribute.ArgumentList.Arguments)
                        {
                            if(item.NameEquals.Name.Identifier.ValueText == "ZeroValueChecker")
                            {
                                if(item.Expression.NormalizeWhitespace().ToFullString() == "\"True\"")
                                {
                                    elseStatement = VALUE_CHECKER;
                                    zeroCheckerDeclaration = VALUE_DECLARATION;
                                    break;
                                }
                            }
                        }
                    }                  

                    StringBuilder sb = new StringBuilder();


                    sb.Append($@"
using System;
using System.ComponentModel.DataAnnotations;

namespace {compiledNamespace}
{{
    [AttributeUsage(AttributeTargets.Property)]
    public class {className} : ValidationAttribute
    {{
        {zeroCheckerDeclaration}

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {{
            if (value == null)
                return new ValidationResult(string.Format(""{{0}} is required"", validationContext.DisplayName));

            if (string.IsNullOrEmpty(value.ToString()))
                            return new ValidationResult(string.Format(""{{0}} is required"", validationContext.DisplayName));

            {property.Type} amount = 0;

            if (!{property.Type}.TryParse(value.ToString(), out amount))
            {{
                return new ValidationResult(string.Format(""{{0}} is invalid"", validationContext.DisplayName));
            }}
            {elseStatement}

            return ValidationResult.Success;
        }}
    }}
}}
            ");

                    context.AddSource($"{className}.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
                }
            }
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            // Register a syntax receiver that will be created for each generation pass.
            // In this case, to detect specific attribute param.
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        /// <summary>
        /// Created on demand before each generation pass
        /// </summary>
        class SyntaxReceiver : ISyntaxReceiver
        {
            public List<PropertyDeclarationSyntax> CandidateFields { get; } = new List<PropertyDeclarationSyntax>();

            /// <summary>
            /// Called for every syntax node in the compilation, we can inspect the nodes and save any information useful for generation
            /// </summary>
            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                // Only handle field syntax with a single attribute
                if (syntaxNode is PropertyDeclarationSyntax propertyDeclarationSyntax
                    && propertyDeclarationSyntax.AttributeLists.Count == 1)
                {
                    // Extract attribute name
                    var firstAttribute = propertyDeclarationSyntax.AttributeLists.First().Attributes.First();
                    var attributeName = firstAttribute.Name.NormalizeWhitespace().ToFullString();

                    // Only save those with attribute name ending with 'Validation'
                    if (attributeName.EndsWith("Validation"))
                        CandidateFields.Add(propertyDeclarationSyntax);
                }
            }
        }
    }
}
