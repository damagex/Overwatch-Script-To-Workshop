﻿using System.Linq;
using System.Collections.Generic;
using Deltin.Deltinteger.Elements;
using Deltin.Deltinteger.Parse.Workshop;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Deltin.Deltinteger.Parse
{
    public class ClassType : CodeType, IScopeAppender
    {
        /// <summary>Used in static methods and returned when ReturningScope() is called. Contains all static members in the inheritance tree.</summary>
        public Scope StaticScope { get; set; }
        /// <summary>Contains all object members in the inheritance tree. Returned when GetObjectScope() is called.</summary>
        public Scope ServeObjectScope { get; set; }
        /// <summary></summary>
        public Scope OperationalScope { get; set; }

        public IVariableInstance[] Variables { get; protected set; }

        // The provider that created this ClassType.
        public IClassInitializer Provider { get; }

        public ClassElements Elements { get; }

        public ClassType(string name, IClassInitializer provider) : base(name)
        {
            Provider = provider;
            Elements = new ClassElements(this);
            Kind = TypeKind.Class;
        }

        public ClassType(string name) : base(name) {}

        public override IWorkshopTree New(ActionSet actionSet, Constructor constructor, WorkshopParameter[] parameters)
        {
            actionSet = actionSet.New(actionSet.IndexAssigner.CreateContained());

            ClassData classData = actionSet.Translate.DeltinScript.GetComponent<ClassData>();

            // Classes are stored in the class array (`classData.ClassArray`),
            // this stores the index where the new class is created at.
            var classReference = Create(actionSet, classData);

            New(actionSet, new NewClassInfo(classReference, constructor, parameters));

            // Return the reference.
            return classReference.GetVariable();
        }

        public IndexReference Create(ActionSet actionSet, ClassData classData)
        {
            // Classes are stored in the class array (`classData.ClassArray`),
            // this stores the index where the new class is created at.
            var classReference = actionSet.VarCollection.Assign("_new_" + Name + "_class_index", actionSet.IsGlobal, true);

            int classID = actionSet.ToWorkshop.ClassInitializer.ComboFromClassType(this).ID;
            classData.GetClassIndex(classID, classReference, actionSet);

            return classReference;
        }

        protected virtual void New(ActionSet actionSet, NewClassInfo newClassInfo)
        {
            // Parse the constructor.
            newClassInfo.Constructor.Parse(actionSet, newClassInfo.Parameters);
        }

        public override void Delete(ActionSet actionSet, Element reference)
        {
            if (Extends != null && Extends.CanBeDeleted)
                Extends.Delete(actionSet, reference);

            // TODO: delete
            // foreach (ObjectVariable objectVariable in ObjectVariables)
            //     actionSet.AddAction(objectVariable.ArrayStore.SetVariable(
            //         value: 0,
            //         index: reference
            //     ));
        }

        public override void AddObjectVariablesToAssigner(ToWorkshop toWorkshop, IWorkshopTree reference, VarIndexAssigner assigner)
        {
            Extends?.AddObjectVariablesToAssigner(toWorkshop, reference, assigner);

            // Add instance variables to the assigner.
            var classInitializer = toWorkshop.ClassInitializer;
            var combo = classInitializer.ComboFromClassType(this);
            combo.AddVariableInstancesToAssigner(Variables, reference, assigner);
        }

        public override Scope GetObjectScope() => ServeObjectScope;
        public override Scope ReturningScope() => StaticScope;

        public override CompletionItem GetCompletion() => new CompletionItem() {
            Label = Name,
            Kind = CompletionItemKind.Class
        };

        public virtual void AddObjectBasedScope(IMethod function)
        {
            Elements.Add(function, true);
            ServeObjectScope.CopyMethod(function);
        }
        public virtual void AddStaticBasedScope(IMethod function)
        {
            Elements.Add(function, false);
            StaticScope.CopyMethod(function);
        }
        public virtual void AddObjectBasedScope(IVariableInstance variable)
        {
            Elements.Add(variable, true);
            ServeObjectScope.CopyVariable(variable);
        }
        public virtual void AddStaticBasedScope(IVariableInstance variable)
        {
            Elements.Add(variable, false);
            StaticScope.CopyVariable(variable);
        }
    
        public class ClassElements
        {
            readonly ClassType _classType;
            readonly List<IMethod> _virtualMethods = new List<IMethod>();
            readonly List<IVariableInstance> _virtualVariables = new List<IVariableInstance>();
            readonly List<ClassElement> _scopeableElements = new List<ClassElement>();
            public IReadOnlyList<ClassElement> ScopeableElements => _scopeableElements.AsReadOnly();

            public ClassElements(ClassType classType) => _classType = classType;

            // Adds a virtual function.
            public void AddVirtualFunction(IMethod method) => _virtualMethods.Add(method);

            public void AddVirtualVariable(IVariableInstance variable) => _virtualVariables.Add(variable);

            // Gets a virtual function. Returns null if none are found.
            public IMethod GetVirtualFunction(DeltinScript deltinScript, string name, CodeType[] parameterTypes)
            {
                // Loop through each virtual function.
                foreach (var virtualFunction in _virtualMethods)
                    // If the function's name matches and the parameter lengths are the same.
                    if (virtualFunction.Name == name && parameterTypes.Length == virtualFunction.Parameters.Length)
                    {
                        bool matches = true;
                        // Loop though the parameters.
                        for (int i = 0; i < parameterTypes.Length; i++)
                            // Make sure the parameter types match.
                            if (!parameterTypes[i].Is(virtualFunction.Parameters[i].GetCodeType(deltinScript)))
                            {
                                matches = false;
                                break;
                            }
                        
                        if (matches)
                            return virtualFunction;
                    }
                
                if (_classType.Extends != null) return ((ClassType)_classType.Extends).Elements.GetVirtualFunction(deltinScript, name, parameterTypes);
                return null;
            }

            // Get the virtual variable that matches the name. Returns null if none are found.
            public IVariableInstance GetVirtualVariable(string name) => _virtualVariables.FirstOrDefault(v => v.Name == name)
                // If it is not found, try again with the extended type if it exists.
                ?? (_classType.Extends as ClassType)?.Elements.GetVirtualVariable(name);

            // Adds an element to the class.
            public void Add(IScopeable scopeable, bool instance) => _scopeableElements.Add(new ClassElement(scopeable, instance));

            // Adds the elements in the class to a scope.
            public void AddToScope(Scope scope, bool instance)
            {
                // Add elements from this class.
                foreach (var scopeable in _scopeableElements)
                    if (ValidAccessLevel(scopeable.Scopeable.AccessLevel) && (!scopeable.IsInstance || instance))
                        scope.AddNative(scopeable.Scopeable);
                
                // Add parent elements.
                (_classType.Extends as ClassType)?.Elements.AddToScope(scope, instance);
            }

            static bool ValidAccessLevel(AccessLevel accessLevel) => accessLevel == AccessLevel.Public || accessLevel == AccessLevel.Protected;

            public struct ClassElement
            {
                public IScopeable Scopeable;
                public bool IsInstance;

                public ClassElement(IScopeable scopeable, bool isInstance)
                {
                    Scopeable = scopeable;
                    IsInstance = isInstance;
                }
            }
        }
    }
}