using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using DotVVM.Framework.Binding;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Binding.Properties;
using DotVVM.Framework.Compilation;
using DotVVM.Framework.Compilation.Binding;
using DotVVM.Framework.Compilation.ControlTree;
using DotVVM.Framework.Compilation.ControlTree.Resolved;
using DotVVM.Framework.Compilation.Javascript;
using DotVVM.Framework.Compilation.Javascript.Ast;
using DotVVM.Framework.Configuration;
using DotVVM.Framework.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace DotVVM.Framework.Testing
{
    /// <summary> Helper class for creating DotVVM bindings. </summary>
    public class BindingTestHelper
    {
        public BindingTestHelper(
            DotvvmConfiguration? configuration = null,
            BindingCompilationService? bindingService = null,
            BindingExtensionParameter[]? defaultExtensionParameters = null)
        {
            Configuration = configuration ?? DotvvmTestHelper.DefaultConfig;
            BindingService = bindingService ?? Configuration.ServiceProvider.GetRequiredService<BindingCompilationService>();
            JavascriptTranslator = Configuration.ServiceProvider.GetRequiredService<JavascriptTranslator>();
            ExpressionBuilder = Configuration.ServiceProvider.GetRequiredService<IBindingExpressionBuilder>();
            DefaultExtensionParameters = defaultExtensionParameters ?? new BindingExtensionParameter[] {
                new CurrentCollectionIndexExtensionParameter(),
                new BindingCollectionInfoExtensionParameter("_collection"),
                new BindingPageInfoExtensionParameter(),
                new BindingApiExtensionParameter(),
            };
        }

        public DotvvmConfiguration Configuration { get; }
        public BindingCompilationService BindingService { get; }
        public BindingExtensionParameter[] DefaultExtensionParameters { get; }
        public JavascriptTranslator JavascriptTranslator { get; }
        public IBindingExpressionBuilder ExpressionBuilder { get; }

        /// <summary> Created a <see cref="DataContextStack" /> from a hierarchy of contexts. First element will be <c>_root</c>, last element will be <c>_this</c>.
        /// Additional extension parameters will be placed in the root context
        /// If <paramref name="markupControl" /> is specified, a <c>_control</c> parameter will be added. </summary>
        public DataContextStack CreateDataContext(Type[] contexts, BindingExtensionParameter[]? extensionParameters = null, Type? markupControl = null)
        {
            var ep = new List<BindingExtensionParameter>();
            if (extensionParameters is {})
                ep.AddRange(extensionParameters);
            if (markupControl is {})
                ep.Add(new CurrentMarkupControlExtensionParameter(new ResolvedTypeDescriptor(markupControl)));
            ep.AddRange(DefaultExtensionParameters);
            ep.AddRange(Configuration.Markup.DefaultExtensionParameters);

            var context = DataContextStack.Create(
                contexts.FirstOrDefault() ?? typeof(object),
                extensionParameters: ep.ToArray()
            );
            for (int i = 1; i < contexts.Length; i++)
            {
                context = DataContextStack.Create(contexts[i], context);
            }
            return context;
        }

        /// <summary> Parses and resolves a binding expression into a System.Linq.Expression using the DotVVM binding parser. </summary>
        /// <param name="expectedType"> If specified, an implicit conversion into this type will be applied in the expression. </param>
        public Expression ParseBinding(string expression, DataContextStack context, Type? expectedType = null, NamespaceImport[]? imports = null)
        {
            expectedType ??= typeof(object);
            var parsedExpression = ExpressionBuilder.ParseWithLambdaConversion(expression, context, BindingParserOptions.Value.AddImports(imports).AddImports(context.NamespaceImports), expectedType);
            return
                TypeConversion.MagicLambdaConversion(parsedExpression, expectedType) ??
                TypeConversion.EnsureImplicitConversion(parsedExpression, expectedType, allowToString: true)!;
        }

        /// <summary> Returns JavaScript code to which the <paramref name="expression" /> translates. </summary>
        /// <param name="contexts"> Hierarchy of data contexts. First element is <c>_root</c>, last element is <c>_this</c>. </param>
        /// <param name="expectedType"> If specified, an implicit conversion into this type will be applied in the expression. </param>
        /// <param name="nullChecks"> If false, the result expression will omit null propagation checks. This slightly improves resulting code readability. </param>
        /// <param name="niceMode"> Whether the expression should contain formatting whitespace. </param>
        public ParametrizedCode ValueBindingToParametrizedCode(string expression, Type[] contexts, Type? expectedType = null, NamespaceImport[]? imports = null, bool nullChecks = false, bool niceMode = true) =>
            ValueBindingToParametrizedCode(expression, CreateDataContext(contexts), expectedType, imports, nullChecks, niceMode);
        /// <summary> Returns JavaScript code to which the <paramref name="expression" /> translates. </summary>
        /// <param name="expectedType"> If specified, an implicit conversion into this type will be applied in the expression. </param>
        /// <param name="nullChecks"> If false, the result expression will omit null propagation checks. This slightly improves resulting code readability. </param>
        /// <param name="niceMode"> Whether the expression should contain formatting whitespace. </param>
        public ParametrizedCode ValueBindingToParametrizedCode(string expression, DataContextStack context, Type? expectedType = null, NamespaceImport[]? imports = null, bool nullChecks = false, bool niceMode = true)
        {
            expectedType ??= typeof(object);

            var expressionTree = ParseBinding(expression, context, expectedType, imports);
            var jsExpression = JavascriptTranslator.CompileToJavascript(expressionTree, context);
            return BindingPropertyResolvers.FormatJavascript(jsExpression, allowObservableResult: true, nullChecks: nullChecks, niceMode: niceMode);
        }

        /// <summary> Returns JavaScript code to which the <paramref name="expression" /> translates. </summary>
        /// <param name="contexts"> Hierarchy of data contexts. First element is <c>_root</c>, last element is <c>_this</c>. </param>
        /// <param name="expectedType"> If specified, an implicit conversion into this type will be applied in the expression. </param>
        /// <param name="nullChecks"> If false, the result expression will omit null propagation checks. This slightly improves resulting code readability. </param>
        /// <param name="niceMode"> Whether the expression should contain formatting whitespace. </param>
        public string ValueBindingToJs(string expression, Type[] contexts, Type? expectedType = null, NamespaceImport[]? imports = null, bool nullChecks = false, bool niceMode = true) =>
            JavascriptTranslator.FormatKnockoutScript(
                ValueBindingToParametrizedCode(expression, contexts, expectedType, imports, nullChecks, niceMode)
            );
        /// <summary> Returns JavaScript code to which the <paramref name="expression" /> translates. </summary>
        /// <param name="expectedType"> If specified, an implicit conversion into this type will be applied in the expression. </param>
        /// <param name="nullChecks"> If false, the result expression will omit null propagation checks. This slightly improves resulting code readability. </param>
        /// <param name="niceMode"> Whether the expression should contain formatting whitespace. </param>
        public string ValueBindingToJs(string expression, DataContextStack context, Type? expectedType = null, NamespaceImport[]? imports = null, bool nullChecks = false, bool niceMode = true) =>
            JavascriptTranslator.FormatKnockoutScript(
                ValueBindingToParametrizedCode(expression, context, expectedType, imports, nullChecks, niceMode)
            );

        /// <summary> Creates a value binding by parsing the specified expression. The expression will be implicitly converted to <typeparamref name="T"/> </summary>
        /// <param name="contexts"> Hierarchy of data contexts. First element is <c>_root</c>, last element is <c>_this</c>. </param>
        public ValueBindingExpression<T> ValueBinding<T>(string expression, Type[] contexts, Type? expectedType = null) =>
            this.ValueBinding<T>(expression, CreateDataContext(contexts));
        /// <summary> Creates a value binding by parsing the specified expression. The expression will be implicitly converted to <typeparamref name="T"/> </summary>
        public ValueBindingExpression<T> ValueBinding<T>(string expression, DataContextStack dataContext, Type? expectedType = null)
        {
            if (expectedType is {} && !typeof(T).IsAssignableFrom(expectedType))
                throw new ArgumentException($"The expected type {expectedType} must be assignable to {typeof(T)}", nameof(expectedType));

            return new ValueBindingExpression<T>(BindingService, new object[] {
                dataContext,
                new OriginalStringBindingProperty(expression),
                BindingParserOptions.Value.AddImports(dataContext.NamespaceImports).AddImports(Configuration.Markup.ImportedNamespaces),
                new ExpectedTypeBindingProperty(expectedType ?? typeof(T))
            });
        }

        /// <summary> Creates a staticCommand binding by parsing the specified expression. </summary>
        /// <param name="contexts"> Hierarchy of data contexts. First element is <c>_root</c>, last element is <c>_this</c>. </param>
        /// <param name="expectedType"> If specified, an implicit conversion into this type will be applied in the expression. </param>
        public StaticCommandBindingExpression StaticCommand(string expression, Type[] contexts, Type? expectedType = null) =>
            StaticCommand(expression, CreateDataContext(contexts), expectedType);
        /// <summary> Creates a staticCommand binding by parsing the specified expression. </summary>
        /// <param name="expectedType"> If specified, an implicit conversion into this type will be applied in the expression. </param>
        public StaticCommandBindingExpression StaticCommand(string expression, DataContextStack context, Type? expectedType = null)
        {
            expectedType ??= typeof(Command);
            return new StaticCommandBindingExpression(BindingService, new object[] {
                context,
                new OriginalStringBindingProperty(expression),
                BindingParserOptions.Value.AddImports(context.NamespaceImports).AddImports(Configuration.Markup.ImportedNamespaces),
                new ExpectedTypeBindingProperty(expectedType)
            });
        }

        /// <summary> Creates a <c>command</c> binding by parsing the specified expression. </summary>
        /// <param name="contexts"> Hierarchy of data contexts. First element is <c>_root</c>, last element is <c>_this</c>. </param>
        /// <param name="expectedType"> If specified, an implicit conversion into this type will be applied in the expression. </param>
        public CommandBindingExpression Command(string expression, Type[] contexts, Type? expectedType = null) =>
            Command(expression, CreateDataContext(contexts), expectedType);

        /// <summary> Creates a <c>command</c> binding by parsing the specified expression. </summary>
        /// <param name="expectedType"> If specified, an implicit conversion into this type will be applied in the expression. </param>
        public CommandBindingExpression Command(string expression, DataContextStack context, Type? expectedType = null)
        {
            expectedType ??= typeof(Command);
            return new CommandBindingExpression(BindingService, new object[] {
                context,
                new OriginalStringBindingProperty(expression),
                BindingParserOptions.Value.AddImports(context.NamespaceImports).AddImports(Configuration.Markup.ImportedNamespaces),
                new ExpectedTypeBindingProperty(expectedType)
            });
        }

        /// <summary> Creates a value binding by parsing the specified expression. The expression will be implicitly converted to <typeparamref name="T"/> </summary>
        /// <param name="contexts"> Hierarchy of data contexts. First element is <c>_root</c>, last element is <c>_this</c>. </param>
        /// <param name="expectedType"> Convert the result to this type instead of converting it to <typeparamref name="T"/>. The type must be assignable to T. </param>
        public ResourceBindingExpression<T> ResourceBinding<T>(string expression, Type[] contexts, Type? expectedType = null) =>
            this.ResourceBinding<T>(expression, CreateDataContext(contexts), expectedType);

        /// <summary> Creates a resource binding by parsing the specified expression. The expression will be implicitly converted to <typeparamref name="T"/> </summary>
        /// <param name="expectedType"> Convert the result to this type instead of converting it to <typeparamref name="T"/>. The type must be assignable to T. </param>
        public ResourceBindingExpression<T> ResourceBinding<T>(string expression, DataContextStack dataContext, Type? expectedType = null)
        {
            if (expectedType is {} && !typeof(T).IsAssignableFrom(expectedType))
                throw new ArgumentException($"The expected type {expectedType} must be assignable to {typeof(T)}", nameof(expectedType));
            return new ResourceBindingExpression<T>(BindingService, new object[] {
                dataContext,
                new OriginalStringBindingProperty(expression),
                BindingParserOptions.Resource.AddImports(dataContext.NamespaceImports).AddImports(Configuration.Markup.ImportedNamespaces),
                new ExpectedTypeBindingProperty(expectedType ?? typeof(T))
            });
        }

        public T ExecuteBinding<T>(
            string expression, object[] viewModelStack,
            DataContextStack? dataContextType = null,
            NamespaceImport[]? imports = null,
            DotvvmControl? markupControl = null,
            Type? expectedType = null
        )
        {
            if (dataContextType is null)
            {
                if (viewModelStack.Contains(null))
                    throw new Exception("dataContextType argument must be specified if any of the view models is null");
                dataContextType ??= CreateDataContext(viewModelStack.Select(v => v!.GetType()).ToArray(), markupControl: markupControl?.GetType());
            }
            if (imports is {})
                dataContextType = DataContextStack.Create(dataContextType.DataContextType, dataContextType.Parent, Enumerable.Concat(imports, dataContextType.NamespaceImports).ToArray(), dataContextType.ExtensionParameters, dataContextType.BindingPropertyResolvers);
            var binding = ResourceBinding<T>(expression, dataContextType, expectedType: expectedType);
            var rootControl = new PlaceHolder();
            if (markupControl is {})
                markupControl.Children.Add(rootControl);

            try
            {
                var control = BuildFakeControlHierarchy(dataContextType, viewModelStack, rootControl);
                return binding.BindingDelegate.Invoke(control);
            }
            finally
            {
                markupControl?.Children.Remove(rootControl);
            }

        }

        DotvvmControl BuildFakeControlHierarchy(DataContextStack contextType, object[] contexts, DotvvmControl rootControl)
        {
            var types = contextType.EnumerableItems().Reverse().ToArray();
            DotvvmControl parent = rootControl;
            if (types.Length != contexts.Length)
                throw new Exception($"{contextType} does not match real types: {string.Join(", ", contexts.Select(t => t?.GetType().Name))}");
            for (int i = 0; i < types.Length; i++)
            {
                var c = new PlaceHolder();
                parent.Children.Add(c);
                c.DataContext = contexts[i];
                c.SetDataContextType(types[i]);
                parent = c;
            }
            return parent;
        }


        /// <summary> Returns the "body" of the JavaScript code produced by the staticCommand.
        /// The method is intended for asserting that the generated code is equal to the correct thing, if you intend to execute the code, please use <see cref="KnockoutHelper.GenerateClientPostBackExpression" /> directly. </summary>
        public static string GetStaticCommandJavascriptBody(StaticCommandBindingExpression binding, bool stripBoilerplate = true)
        {
            var expr = KnockoutHelper.GenerateClientPostBackExpression(
                "NonExistentProperty",
                binding,
                new Literal(),
                new PostbackScriptOptions(
                    allowPostbackHandlers: false,
                    returnValue: null,
                    commandArgs: CodeParameterAssignment.FromIdentifier("commandArguments")
                ));
            if (stripBoilerplate)
            {
                expr = strip(expr, "dotvvm.applyPostbackHandlers(", ",this,[],commandArguments)");
                expr = strip(expr, "async", "");
                expr = strip(expr, "(options)", "");
                expr = strip(expr, "=>", "");
            }
            return expr;

            string strip(string value, string start, string end)
            {
                if (value.StartsWith(start) && value.EndsWith(end))
                    return value.Substring(start.Length, value.Length - start.Length - end.Length).Trim();
                else
                    return value;
            }
        }
    }
}
