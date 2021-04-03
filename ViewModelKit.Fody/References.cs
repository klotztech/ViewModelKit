using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace ViewModelKit.Fody
{
    /// <summary>
    /// Locates and provides references to commonly used types and members.
    /// </summary>
    internal class References
    {
        public AssemblyNameReference PrismAssemblyNameReference;
        public AssemblyNameReference ViewModelKitAssemblyNameReference;
        public TypeReference PropChangedInterfaceReference;
        public TypeReference PropChangedHandlerReference;
        public MethodReference ComponentModelPropertyChangedEventHandlerInvokeReference;
        public MethodReference ComponentModelPropertyChangedEventConstructorReference;
        public MethodReference CompilerGeneratedAttributeConstructorReference;

        // Prism References
        public TypeReference DelegateCommandType;
        public MethodReference DelegateCommandRaiseCanExecuteChangedMethodReference;
        public MethodReference DelegateCommandActionConstructor;

        // Linq References
        public MethodReference ConstantExpressionMethod;
        public MethodReference MemberExpressionMethod;
        public TypeReference ParameterExpressionType;

        public TypeDefinition ViewModelBaseType;
        public TypeDefinition ValidatingViewModelBaseType;
        //public TypeDefinition ValidatingObservableCollectionType;   // TODO: Generic types can't be copied yet, see CecilExtensions.CopyToModule
        public TypeDefinition InputCleanupType;
        public Dictionary<IMemberDefinition, IMemberDefinition> MemberMap = new Dictionary<IMemberDefinition, IMemberDefinition>();

        private ModuleDefinition moduleDef;
        private TypeDefinition equalityComparerDefinition;

        /// <summary>
        /// Initialises a new instance of the <see cref="References"/> class.
        /// </summary>
        /// <param name="moduleDef">The target module.</param>
        public References(ModuleDefinition moduleDef)
        {
            this.moduleDef = moduleDef;

            var systemDefinition = moduleDef.AssemblyResolver.Resolve("System");
            var systemTypes = systemDefinition.MainModule.Types;

            var propChangedInterfaceDefinition = systemTypes.FirstOrDefault(x => x.Name == "INotifyPropertyChanged") ?? throw new Exception("INotifyPropertyChanged not found");
            PropChangedInterfaceReference = moduleDef.ImportReference(propChangedInterfaceDefinition);
            var propChangedHandlerDefinition = systemTypes.FirstOrDefault(x => x.Name == "PropertyChangedEventHandler") ?? throw new Exception("PropertyChangedEventHandler not found");
            PropChangedHandlerReference = moduleDef.ImportReference(propChangedHandlerDefinition);
            ComponentModelPropertyChangedEventHandlerInvokeReference = moduleDef.ImportReference(propChangedHandlerDefinition.Methods.FirstOrDefault(x => x.Name == "Invoke"))
                ?? throw new Exception("PropertyChangedEventHandler.Invoke() not found");
            var propChangedArgsDefinition = systemTypes.FirstOrDefault(x => x.Name == "PropertyChangedEventArgs") ?? throw new Exception("PropertyChangedEventArgs not found");
            ComponentModelPropertyChangedEventConstructorReference = moduleDef.ImportReference(propChangedArgsDefinition.Methods.FirstOrDefault(x => x.IsConstructor)
                ?? throw new Exception("new PropertyChangedEventArgs() not found"));

            var msCoreLibDefinition = moduleDef.AssemblyResolver.Resolve("mscorlib");
            var msCoreTypes = msCoreLibDefinition.MainModule.Types;

            var compilerGeneratedAttributeDefinition = msCoreTypes.FirstOrDefault(x => x.Name == "CompilerGeneratedAttribute")
                ?? throw new Exception("CompilerGeneratedAttribute not found");
            CompilerGeneratedAttributeConstructorReference = moduleDef.ImportReference(compilerGeneratedAttributeDefinition.Methods.First(x => x.IsConstructor)
                ?? throw new Exception("new CompilerGeneratedAttribute() not found"));

            equalityComparerDefinition = msCoreTypes.FirstOrDefault(x => x.FullName == "System.Collections.Generic.EqualityComparer`1")
                ?? throw new Exception("System.Collections.Generic.EqualityComparer`1 not found");


            PrismAssemblyNameReference = moduleDef.AssemblyReferences.FirstOrDefault(r => r.Name == "Prism");
            if (PrismAssemblyNameReference == null)
                throw new Exception("Prism assembly is not referenced from the processed assembly.");

            var prismModuleDef = moduleDef.AssemblyResolver.Resolve(PrismAssemblyNameReference).MainModule;
            var oldDelegateCommandType = prismModuleDef.Types.FirstOrDefault(t => t.FullName == "Prism.Commands.DelegateCommand")
                ?? throw new Exception("Prism.Commands.DelegateCommand not found");
            DelegateCommandType = moduleDef.ImportReference(oldDelegateCommandType);

            // new DelegateCommand(Action action)
            //DelegateCommandActionConstructor = oldDelegateCommandType.GetConstructors().FirstOrDefault(c => !c.IsStatic && c.Parameters.Count == 1 && c.Parameters[0].ParameterType.);

            var commandBaseTypeRef = prismModuleDef.Types.FirstOrDefault(t => t.FullName == "Prism.Commands.DelegateCommandBase")
                ?? throw new Exception("Prism.Commands.DelegateCommandBase not found");

            var raiseCanExecuteChangedMethod = commandBaseTypeRef.Methods.FirstOrDefault(m => m.Name == "RaiseCanExecuteChanged")
                ?? throw new Exception("RaiseCanExecuteChanged not found: " + string.Join(",", commandBaseTypeRef.Methods.Select(m => m.FullName)));
            DelegateCommandRaiseCanExecuteChangedMethodReference = moduleDef.ImportReference(raiseCanExecuteChangedMethod);

            ViewModelKitAssemblyNameReference = moduleDef.AssemblyReferences.FirstOrDefault(r => r.Name == "ViewModelKit");
            if (ViewModelKitAssemblyNameReference == null)
                throw new Exception("ViewModelKit assembly is not referenced from the processed assembly.");

            var vmkModuleDef = moduleDef.AssemblyResolver.Resolve(ViewModelKitAssemblyNameReference).MainModule;
            var oldViewModelBaseType = vmkModuleDef.Types.FirstOrDefault(t => t.FullName == "ViewModelKit.ViewModelBase") ?? throw new Exception("ViewModelKit.ViewModelBase not found");
            //var oldDelegateCommandType = vmkModuleDef.Types.FirstOrDefault(t => t.FullName == "ViewModelKit.DelegateCommand") ?? throw new Exception("ViewModelKit.DelegateCommand not found");
            var oldValidatingViewModelBaseType = vmkModuleDef.Types.FirstOrDefault(t => t.FullName == "ViewModelKit.ValidatingViewModelBase") ?? throw new Exception("ViewModelKit.ValidatingViewModelBase not found");
            //var oldValidatingObservableCollectionType = vmkModuleDef.Types.First(t => t.FullName == "ViewModelKit.ValidatingObservableCollection`1");
            var oldInputCleanupType = vmkModuleDef.Types.FirstOrDefault(t => t.FullName == "ViewModelKit.InputCleanup") ?? throw new Exception("ViewModelKit.InputCleanup not found");
            ViewModelBaseType = oldViewModelBaseType.CopyToModule(moduleDef, ref MemberMap, "<VMK>ViewModelBase", "");
            //DelegateCommandType = oldDelegateCommandType.CopyToModule(moduleDef, ref MemberMap, "<VMK>DelegateCommand", "");
            ValidatingViewModelBaseType = oldValidatingViewModelBaseType.CopyToModule(moduleDef, ref MemberMap, "<VMK>ValidatingViewModelBase", "");
            //ValidatingObservableCollectionType = oldValidatingObservableCollectionType.CopyToModule(moduleDef, ref MemberMap, "<VMK>ValidatingObservableCollection`1", "");
            InputCleanupType = oldInputCleanupType.CopyToModule(moduleDef, ref MemberMap, "<VMK>InputCleanup", "");


            // Linq Stuff
            var systemCoreDefinition = moduleDef.AssemblyResolver.Resolve("System.Core");
            var systemCoreTypes = systemCoreDefinition.MainModule.Types;
            var linqExpression = systemCoreTypes.FirstOrDefault(x => x.FullName == "System.Linq.Expressions.Expression")
                ?? throw new Exception("System.Linq.Expressions.Expression not found");

            var constantExprMethod = linqExpression.Methods.FirstOrDefault(m => m.IsStatic && m.Name == "Constant" && m.Parameters.Count == 2
                && m.Parameters[0].ParameterType.FullName == "System.Object"
                && m.Parameters[1].ParameterType.FullName == "System.Type")
                ?? throw new Exception("System.Linq.Expressions.Expression.Constant(object, Type) not found");

            ConstantExpressionMethod = moduleDef.ImportReference(constantExprMethod);

            var memberExprMethod = linqExpression.Methods.FirstOrDefault(m => m.IsStatic && m.Name == "Property" && m.Parameters.Count == 2
                && m.Parameters[0].ParameterType.FullName == "System.Linq.Expressions.Expression"
                && m.Parameters[1].ParameterType.FullName == "System.Reflection.MethodInfo")
                ?? throw new Exception("System.Linq.Expressions.Expression.Property(Expression, MethodInfo) not found");

            MemberExpressionMethod = moduleDef.ImportReference(memberExprMethod);

            var parameterExpression = systemCoreTypes.FirstOrDefault(x => x.FullName == "System.Linq.Expressions.ParameterExpression")
                ?? throw new Exception("System.Linq.Expressions.ParameterExpression not found");
            ParameterExpressionType = moduleDef.ImportReference(parameterExpression);
        }

        public MethodReference EqualityComparerDefaultReference(TypeReference genericType)
        {
            var genType = new GenericInstanceType(equalityComparerDefinition);
            genType.GenericArguments.Add(genericType);
            var importedGenType = moduleDef.ImportReference(genType);
            var getDefaultMethodDefinition = importedGenType.Resolve()
                .Methods.First(x =>
                    x.Name == "get_Default" &&
                    x.IsStatic);
            var equalityComparerDefaultReference = moduleDef.ImportReference(getDefaultMethodDefinition);
            equalityComparerDefaultReference.DeclaringType = importedGenType;
            return equalityComparerDefaultReference;
        }

        public MethodReference EqualityComparerEqualsReference(TypeReference genericType)
        {
            var genType = new GenericInstanceType(equalityComparerDefinition);
            genType.GenericArguments.Add(genericType);
            var importedGenType = moduleDef.ImportReference(genType);
            var equalsMethodDefinition = importedGenType.Resolve()
                .Methods.First(x =>
                    x.Name == "Equals" &&
                    !x.IsStatic);
            var equalityComparerEqualsReference = moduleDef.ImportReference(equalsMethodDefinition);
            equalityComparerEqualsReference.DeclaringType = importedGenType;
            return equalityComparerEqualsReference;
        }

        public MethodReference DelegateCommandConstructor(TypeReference commandType, params TypeReference[] parameterTypes)
        {
            //var prismModuleDef = moduleDef.AssemblyResolver.Resolve(PrismAssemblyNameReference).MainModule;
            //var oldDelegateCommandType = prismModuleDef.Types.FirstOrDefault(t => t.FullName == "Prism.Commands.DelegateCommand")
            //    ?? throw new Exception("Prism.Commands.DelegateCommand not found");

            var ctors = from c in commandType.Resolve().GetConstructors()
                        where !c.IsStatic
                            && c.Parameters.Count == parameterTypes.Length
                            && c.Parameters.Zip(parameterTypes, (cp, pt) => cp.ParameterType.Resolve().IsAssignableFrom(pt.Resolve())).All(b => b)
                        select c;

            var ctor = ctors.FirstOrDefault()
                ?? throw new Exception($"new DelegateCommand({string.Join(", ", parameterTypes.Select(p => p.FullName))}) not found:"
                 + string.Join("\r\n",
                    from c in commandType.Resolve().GetConstructors()
                    where !c.IsStatic
                        && c.Parameters.Count == parameterTypes.Length
                    select $"FullName={c.FullName} Parameters=[{string.Join(", ", from p in c.Parameters select p.ParameterType.FullName)}]"));

            MethodReference ctorRef = moduleDef.ImportReference(ctor);

            // Add back commands type parameters to imported ctor reference
            if (commandType is GenericInstanceType genericCommand)
                return ctorRef.MakeHostInstanceGeneric(args: genericCommand.GenericArguments.ToArray());

            return ctorRef;
        }

        public MethodReference GenericActionConstructor(TypeReference typeParameter)
        {
            var actionRef = moduleDef.ImportReference(typeof(Action<>));
            var genType = new GenericInstanceType(actionRef);
            genType.GenericArguments.Add(typeParameter);
            var importedGenType = moduleDef.ImportReference(genType);
            var ctor = importedGenType.Resolve()
                .GetConstructors().FirstOrDefault(x => !x.IsStatic)
                ?? throw new Exception($"new Action<{typeParameter.FullName}>() not found");
            return moduleDef.ImportReference(ctor).MakeHostInstanceGeneric(typeParameter);
        }

        public MethodReference GenericPredicateFuncConstructor(TypeReference typeParameter)
        {
            var actionRef = moduleDef.ImportReference(typeof(Func<,>));
            var boolRef = moduleDef.ImportReference(typeof(bool));
            var genType = new GenericInstanceType(actionRef);
            genType.GenericArguments.Add(typeParameter);
            genType.GenericArguments.Add(boolRef);
            var importedGenType = moduleDef.ImportReference(genType);
            var equalsMethodDefinition = importedGenType.Resolve()
                .GetConstructors().FirstOrDefault(x => !x.IsStatic)
                ?? throw new Exception($"new Func<{typeParameter.FullName},bool>() not found");
            return moduleDef.ImportReference(equalsMethodDefinition);
        }

        public MethodReference GenericEmptyArrayMethod(TypeReference typeParameter)
        {
            var arrayRef = moduleDef.ImportReference(typeof(Array));
            var emptyRef = arrayRef.Resolve().Methods.FirstOrDefault(m => m.IsStatic && m.HasGenericParameters && m.Name == "Empty" && !m.HasParameters)
                ?? throw new Exception($"System.Array.Empty<{typeParameter.FullName}>() not found");
            var genMeth = new GenericInstanceMethod(emptyRef);
            genMeth.GenericArguments.Add(typeParameter);
            return moduleDef.ImportReference(genMeth);
        }

        public MethodReference GenericLambdaMethod(TypeReference typeParameter)
        {
            var systemCoreDefinition = moduleDef.AssemblyResolver.Resolve("System.Core");
            var systemCoreTypes = systemCoreDefinition.MainModule.Types;
            var linqExpression = systemCoreTypes.FirstOrDefault(x => x.FullName == "System.Linq.Expressions.Expression")
                ?? throw new Exception("System.Linq.Expressions.Expression not found");

            var arrayRef = moduleDef.ImportReference(typeof(Array));
            var lambdaRef = linqExpression.Methods.FirstOrDefault(m => m.IsStatic && m.HasGenericParameters && m.Name == "Lambda"
                && m.HasParameters && m.Parameters.Count == 2
                && m.Parameters[0].ParameterType.FullName == "System.Linq.Expressions.Expression"
                && m.Parameters[1].ParameterType.FullName == "System.Linq.Expressions.ParameterExpression[]")
                ?? throw new Exception($"System.Linq.Expressions.Expression.Lambda<>() not found");

            var funcRef = moduleDef.ImportReference(typeof(Func<>));
            var genType = new GenericInstanceType(funcRef);
            genType.GenericArguments.Add(typeParameter);
            var importedGenericFunc = moduleDef.ImportReference(genType);

            var genMeth = new GenericInstanceMethod(lambdaRef);
            genMeth.GenericArguments.Add(importedGenericFunc);
            return moduleDef.ImportReference(genMeth);
        }

        public MethodReference GenericObservesPropertyMethod(TypeReference commandType, TypeReference typeParameter)
        {
            var observesPropertyMethod = commandType.Resolve().Methods.FirstOrDefault(m => !m.IsStatic && m.HasGenericParameters && m.Name == "ObservesProperty"
                && m.HasParameters && m.Parameters.Count == 1
                && m.Parameters[0].ParameterType.FullName.StartsWith("System.Linq.Expressions.Expression"))
                ?? throw new Exception($"{commandType.FullName}.ObservesProperty<{typeParameter.FullName}>() not found");

            var openRef = moduleDef.ImportReference(observesPropertyMethod);
            var genMeth = new GenericInstanceMethod(openRef);
            genMeth.GenericArguments.Add(typeParameter);
            return moduleDef.ImportReference(genMeth);
        }
    }
}
