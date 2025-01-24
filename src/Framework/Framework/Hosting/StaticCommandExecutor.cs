using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using DotVVM.Framework.Compilation.Binding;
using DotVVM.Framework.Configuration;
using DotVVM.Framework.Runtime;
using DotVVM.Framework.Security;
using DotVVM.Framework.Utils;
using DotVVM.Framework.ViewModel;
using DotVVM.Framework.ViewModel.Serialization;
using DotVVM.Framework.ViewModel.Validation;
namespace DotVVM.Framework.Hosting
{
    public class StaticCommandExecutor
    {
#pragma warning disable CS0618
        private readonly IStaticCommandServiceLoader serviceLoader;
        private readonly IViewModelProtector viewModelProtector;
        private readonly IStaticCommandArgumentValidator validator;
        private readonly DotvvmConfiguration configuration;
        private readonly JsonSerializerOptions jsonOptions;

        public StaticCommandExecutor(IStaticCommandServiceLoader serviceLoader, IDotvvmJsonOptionsProvider jsonOptions, IViewModelProtector viewModelProtector, IStaticCommandArgumentValidator validator, DotvvmConfiguration configuration)
        {
            this.serviceLoader = serviceLoader;
            this.viewModelProtector = viewModelProtector;
            this.validator = validator;
            this.configuration = configuration;
            this.jsonOptions = jsonOptions.ViewModelJsonOptions;
        }
#pragma warning restore CS0618

        public StaticCommandInvocationPlan DecryptPlan(byte[] encrypted)
        {
            var decrypted = StaticCommandExecutionPlanSerializer.DecryptJson(encrypted, viewModelProtector);
            var reader = new Utf8JsonReader(decrypted.AsSpan());
            return StaticCommandExecutionPlanSerializer.DeserializePlan(ref reader);
        }
        public Task<object?> Execute(
            StaticCommandInvocationPlan plan,
            JsonElement arguments,
            IEnumerable<string?>? argumentValidationPaths,
            IDotvvmRequestContext context
        ) => Execute(plan, new Queue<JsonElement>(arguments.EnumerateArray()), argumentValidationPaths is null ? null : new Queue<string?>(argumentValidationPaths), context);

        public async Task<object?> Execute(
            StaticCommandInvocationPlan plan,
            Queue<JsonElement> arguments,
            Queue<string?>? argumentValidationPaths,
            IDotvvmRequestContext context
        )
        {
            var parameters = plan.Method.GetParameters();
            object? DeserializeArgument(Type type, int index)
            {
                var parameterType =
                    plan.Method.IsStatic ? parameters[index].ParameterType :
                    index == 0 ? plan.Method.DeclaringType :
                    parameters[index - 1].ParameterType;
                if (!parameterType!.IsAssignableFrom(type))
                    throw new Exception($"Argument {index} has an invalid type");
                var arg = arguments.Dequeue();
                using var state = DotvvmSerializationState.Create(true, context.Services, new System.Text.Json.Nodes.JsonObject());
                return JsonSerializer.Deserialize(arg, type, this.jsonOptions);
            }
            var methodArgs = new List<object?>();
            var methodArgsPaths = argumentValidationPaths is null ? null : new List<string?>();
            foreach (var a in plan.Arguments)
            {
                var index = methodArgs.Count;
                var (value, path) = a.Type switch {
                    StaticCommandParameterType.Argument =>
                        (DeserializeArgument((Type)a.Arg!, index), argumentValidationPaths?.Dequeue()),
                    StaticCommandParameterType.Constant or StaticCommandParameterType.DefaultValue =>
                        (a.Arg, null),
                    StaticCommandParameterType.Inject =>
#pragma warning disable CS0618
                        (serviceLoader.GetStaticCommandService((Type)a.Arg!, context), null),
#pragma warning restore CS0618
                    StaticCommandParameterType.Invocation =>
                        (await Execute((StaticCommandInvocationPlan)a.Arg!, arguments, argumentValidationPaths, context), null),
                    _ => throw new NotSupportedException("" + a.Type)
                };
                methodArgs.Add(value);
                methodArgsPaths?.Add(path);
            }

            var methodAttribute = plan.Method.GetCustomAttribute<AllowStaticCommandAttribute>().NotNull("StaticCommand method must have the AllowStaticCommand attribute.");
            if (methodAttribute.Validation == StaticCommandValidation.Automatic)
            {
                var errors = validator.ValidateStaticCommand(plan, methodArgs.ToArray(), context);
                if (errors is not null)
                {
                    ResolveValidationPaths(errors, plan.Method, methodArgsPaths?.ToArray(), null);
                    throw new DotvvmInvalidStaticCommandModelStateException(errors);
                }
            }

            try
            {
                var result = plan.Method.Invoke(
                    plan.Method.IsStatic ? null : methodArgs.First(),
                    (plan.Method.IsStatic ? methodArgs : methodArgs.Skip(1)).ToArray());

                return await TaskUtils.ToObjectTask(result);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is DotvvmInvalidStaticCommandModelStateException innerEx)
            {
                ResolveValidationPaths(innerEx.StaticCommandModelState, plan.Method, methodArgsPaths?.ToArray(), innerEx);
                throw;
            }
            catch (DotvvmInvalidStaticCommandModelStateException ex)
            {
                ResolveValidationPaths(ex.StaticCommandModelState, plan.Method, methodArgsPaths?.ToArray(), ex);
                throw;
            }
        }

        private void ResolveValidationPaths(StaticCommandModelState state, MethodInfo method, string?[]? argumentPaths, DotvvmInvalidStaticCommandModelStateException? innerException)
        {
            var invokedMethodParameters = method.GetParameters();

            foreach (var error in state.Errors.Where(e => !e.IsResolved))
            {
                if (argumentPaths is null)
                    throw new Exception("Could not respond with validation failure because the client did not send validation paths.", innerException);
                if (error.PropertyPathExtractor != null)
                {
                    var path = error.PropertyPathExtractor(configuration).TrimStart('/');
                    var slashIndex = path.IndexOf('/');
                    var hasPropertySegment = slashIndex >= 0 && slashIndex < path.Length - 1;
                    var name = hasPropertySegment ? path.Substring(0, slashIndex) : path;
                    var rest = hasPropertySegment ? path.Substring(slashIndex + 1) : string.Empty;
                    error.ArgumentName = name;
                    error.PropertyPath = rest;
                }

                int parameterIndex;

                if (invokedMethodParameters.FirstOrDefault(p => p.Name == error.ArgumentName) is {} parameter)
                {
                    parameterIndex = parameter.Position;
                    if (!method.IsStatic)
                        parameterIndex += 1;
                }
                else if (error.ArgumentName == "this" && !method.IsStatic)
                    parameterIndex = 0;
                else
                    throw new ArgumentException($"Could not map argument name \"{error.ArgumentName}\" to any parameter of {ReflectionUtils.FormatMethodInfo(method)}.", innerException);

                var propertyPath = error.PropertyPath?.Trim('/');
                var argumentPath = argumentPaths![parameterIndex]?.TrimEnd('/');
                if (argumentPath is null)
                    throw new StaticCommandMissingValidationPathException(error, innerException);
                error.PropertyPath = $"{argumentPath}/{propertyPath}".TrimEnd('/');
                error.IsResolved = true;
            }
        }

        record StaticCommandMissingValidationPathException(StaticCommandValidationError ValidationError, Exception? InnerException): RecordExceptions.RecordException(InnerException)
        {
            public override string Message => $"Could not serialize validation error {ValidationError.ArgumentName}/{ValidationError.PropertyPath}, the client did not specify the validation path for this method argument. Make sure that the argument maps directly into a view model property (or use AddRawError to sidestep the automagic mapping in advanced cases).";
        }

        public void DisposeServices(IDotvvmRequestContext context)
        {
#pragma warning disable CS0618
            serviceLoader.DisposeStaticCommandServices(context);
#pragma warning restore CS0618
        }

    }
}
