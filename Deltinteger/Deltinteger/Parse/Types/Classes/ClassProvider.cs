using System;
using CompletionItem = OmniSharp.Extensions.LanguageServer.Protocol.Models.CompletionItem;
using CompletionItemKind = OmniSharp.Extensions.LanguageServer.Protocol.Models.CompletionItemKind;

namespace Deltin.Deltinteger.Parse
{
    public interface IClassInitializer : ITypeArgTrackee
    {
        ClassType Extends { get; }
    }

    public abstract class ClassInitializer : ICodeTypeInitializer, IResolveElements, IClassInitializer
    {
        public string Name { get; }
        public AnonymousType[] GenericTypes { get; protected set; }
        public int GenericsCount => GenericTypes.Length;
        public CodeType WorkingInstance { get; protected set; }

        /// <summary>Determines if the class elements were resolved.</summary>
        protected bool _elementsResolved = false;

        public ClassType Extends { get; protected set; }

        public ClassInitializer(string name)
        {
            Name = name;
        }

        public abstract bool BuiltInTypeMatches(Type type);

        public virtual void ResolveElements()
        {
            if (_elementsResolved) return;
            _elementsResolved = true;
            if (Extends != null) ((ClassType)Extends).ResolveElements();
        }

        public virtual CodeType GetInstance() => new ClassType(Name);
        public virtual CodeType GetInstance(GetInstanceInfo instanceInfo) => new ClassType(Name);

        public CompletionItem GetCompletion() => new CompletionItem() {
            Label = Name,
            Kind = CompletionItemKind.Class
        };
    }
}