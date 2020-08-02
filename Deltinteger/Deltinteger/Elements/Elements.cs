﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using Deltin.Deltinteger.LanguageServer;
using Deltin.Deltinteger.WorkshopWiki;
using Deltin.Deltinteger.Parse;
using Deltin.Deltinteger.Models;
using Deltin.Deltinteger.I18n;
using CompletionItem = OmniSharp.Extensions.LanguageServer.Protocol.Models.CompletionItem;
using CompletionItemKind = OmniSharp.Extensions.LanguageServer.Protocol.Models.CompletionItemKind;
using StringOrMarkupContent = OmniSharp.Extensions.LanguageServer.Protocol.Models.StringOrMarkupContent;

namespace Deltin.Deltinteger.Elements
{
    public enum ValueType
    {
        Any,
        VectorAndPlayer,
        Number,
        Boolean,
        Hero,
        Vector,
        Player,
        Team,
        Map,
        Gamemode,
        Button,
        String
    }

    public class NewElement : IWorkshopTree
    {
        public static NewElement Part(string name, params IWorkshopTree[] parameterValues)
            => new NewElement(ElementJsonRoot.Instance.GetFunction(name), parameterValues);
        public static NewElement Part(ElementBaseJson function, params IWorkshopTree[] parameterValues)
            => new NewElement(function, parameterValues);

        public ElementBaseJson Function { get; }
        public IWorkshopTree[] ParameterValues { get; set; }
        public bool Disabled { get; set; }
        public string Comment { get; set; }

        public NewElement(ElementBaseJson function, IWorkshopTree[] parameterValues)
        {
            Function = function;
            ParameterValues = parameterValues;
        }

        public virtual string ToWorkshop(OutputLanguage language, ToWorkshopContext context)
        {            
            string result = "";

            // Add a comment and newline
            if (Comment != null) result += $"\"{Comment}\"\n";

            // Add the disabled tag if the element is disabled.
            if (Function is ElementJsonAction && Disabled) result += LanguageInfo.Translate(language, "disabled") + " ";

            // Add the name of the element.
            result += LanguageInfo.Translate(language, Function.Name);

            // Add the parameters.
            AddMissingParameters();
            var parameters = ParameterValues.Select(p => p.ToWorkshop(language, ToWorkshopContext.NestedValue));
            if (parameters.Count() != 0) result += "(" + string.Join(", ", parameters) + ")";

            return result;
        }

        /// <summary>Makes sure no parameter values are null.</summary>
        private void AddMissingParameters()
        {
            List<IWorkshopTree> parameters = new List<IWorkshopTree>();

            for (int i = 0; i < Function.Parameters.Length || i < ParameterValues.Length; i++)
                parameters.Add(ParameterValues?.ElementAtOrDefault(i) ?? GetDefaultParameter(Function.Parameters[i]));
            
            ParameterValues = parameters.ToArray();
        }

        public virtual bool EqualTo(IWorkshopTree other)
        {
            if (this.GetType() != other.GetType()) return false;

            NewElement bElement = (NewElement)other;
            if (Function != bElement.Function || ParameterValues.Length != bElement.ParameterValues.Length) return false;

            Type[] createsRandom = new Type[] {
                typeof(V_RandomInteger),
                typeof(V_RandomizedArray),
                typeof(V_RandomReal),
                typeof(V_RandomValueInArray)
            };

            for (int i = 0; i < ParameterValues.Length; i++)
            {
                if ((ParameterValues[i] == null) != (bElement.ParameterValues[i] == null))
                    return false;

                if (ParameterValues[i] != null && (!ParameterValues[i].EqualTo(bElement.ParameterValues[i]) || createsRandom.Contains(ParameterValues[i].GetType())))
                    return false;
            }
            
            return true;
        }

        public NewElement Optimize()
        {
            OptimizeChildren();
        }

        protected void OptimizeChildren()
        {
            AddMissingParameters();
            for (int i = 0; i < ParameterValues.Length; i++)
                if (ParameterValues[i] is NewElement element)
                    ParameterValues[i] = element.Optimize();
        }

        public bool TryGetConstant(out Vertex vertex)
        {
            if (Function.Name == "Vector"
                && ParameterValues[0] is NewElement xe && xe.TryGetConstant(out double x)
                && ParameterValues[1] is NewElement ye && ye.TryGetConstant(out double y)
                && ParameterValues[2] is NewElement ze && ze.TryGetConstant(out double z))
            {
                vertex = new Vertex(x, y, z);
                return true;
            }
            vertex = null;
            return false;
        }

        public virtual bool TryGetConstant(out double number)
        {
            number = 0;
            return false;
        }

        public virtual bool TryGetConstant(out bool boolean)
        {
            if (Function.Name == "True")
            {
                boolean = true;
                return true;
            }
            else if (Function.Name == "False")
            {
                boolean = false;
                return true;
            }
            boolean = false;
            return false;
        }

        public static NewElement Compare(IWorkshopTree a, Operators op, IWorkshopTree b)
            => Part("Compare", a, new OperatorElement(op), b);
        
        public static NewElement Vector(NewElement x, NewElement y, NewElement z)
            => Part("Vector", x, y, z);
        
        public static NewElement Pow(NewElement a, NewElement b)
            => Part("Raise To Power", a, b);

        public static NewElement operator +(NewElement a, NewElement b) => Part("Add", a, b);
        public static NewElement operator -(NewElement a, NewElement b) => Part("Subtract", a, b);
        public static NewElement operator *(NewElement a, NewElement b) => Part("Multiply", a, b);
        public static NewElement operator /(NewElement a, NewElement b) => Part("Divide", a, b);
        public static NewElement operator %(NewElement a, NewElement b) => Part("Modulo", a, b);
        public static NewElement operator <(NewElement a, NewElement b) => Compare(a, Operators.LessThan, b);
        public static NewElement operator >(NewElement a, NewElement b) => Compare(a, Operators.GreaterThan, b);
        public static NewElement operator <=(NewElement a, NewElement b) => Compare(a, Operators.LessThanOrEqual, b);
        public static NewElement operator >=(NewElement a, NewElement b) => Compare(a, Operators.GreaterThanOrEqual, b);
        public static NewElement operator !(NewElement a) => Part("Not", a);
        public static NewElement operator -(NewElement a) => a * -1;
        public NewElement this[IWorkshopTree i]
        {
            get => Part("Value In Array", this, i);
            private set {}
        }
        public NewElement this[NewElement i]
        {
            get => Part("Value In Array", this, i);
            private set {}
        }
        public static implicit operator NewElement(double number) => new NumberElement(number);
        public static implicit operator NewElement(int number) => new NumberElement(number);
        public static implicit operator NewElement(bool boolean) => Part(boolean.ToString());

        public static IWorkshopTree GetDefaultParameter(ElementParameter parameter)
        {
            // todo
            return null;
        }

        // Creates an array from a list of values.
        public static NewElement CreateArray(params IWorkshopTree[] values)
        {
            if (values == null || values.Length == 0) return Part("Empty Array");
            return Part("Array", values);
        }

        public static NewElement CreateAppendArray(params IWorkshopTree[] values)
        {
            NewElement array = Part("Empty Array");
            for (int i = 0; i < values.Length; i++)
                array = Part("Append To Array", array, values[i]);
            return array;
        }

        // Creates an ternary conditional that works in the workshop
        public static NewElement TernaryConditional(IWorkshopTree condition, IWorkshopTree consequent, IWorkshopTree alternative) => Part("If-Then-Else", condition, consequent, alternative);
    }

    public class OperatorElement : IWorkshopTree
    {
        public Operators Operator { get; }

        public OperatorElement(Operators op)
        {
            Operator = op;
        }

        public bool EqualTo(IWorkshopTree other) => other is OperatorElement oe && oe.Operator == Operator;

        public string ToWorkshop(OutputLanguage language, ToWorkshopContext context)
        {
            switch (Operator)
            {
                case Operators.Equal: return "==";
                case Operators.GreaterThan: return ">";
                case Operators.GreaterThanOrEqual: return ">=";
                case Operators.LessThan: return "<";
                case Operators.LessThanOrEqual: return "<=";
                case Operators.NotEqual: return "!=";
                default: throw new NotImplementedException();
            }
        }
    }

    public class NumberElement : NewElement
    {
        public double Value { get; }

        public NumberElement(double value) : base(ElementJsonRoot.Instance.GetFunction("Number"), null)
        {
            Value = value;
        }

        public override bool TryGetConstant(out double number)
        {
            number = Value;
            return true;
        }

        public override string ToWorkshop(OutputLanguage language, ToWorkshopContext context)
        {
            return base.ToWorkshop(language, context);
        }

        public override bool EqualTo(IWorkshopTree other) => base.EqualTo(other) && ((NumberElement)other).Value == Value;
    }

    public enum ElementIndent
    {
        Neutral,
        Increment,
        Decrement
    }

    public abstract class Element : IWorkshopTree
    {
        public static T Part<T>(params IWorkshopTree[] parameterValues) where T : Element, new()
        {
            T element = new T()
            {
                ParameterValues = parameterValues
            };
            return element;
        }

        public Element(params IWorkshopTree[] parameterValues)
        {
            ElementList = ElementList.FromType(GetType());
            ElementData = GetType().GetCustomAttribute<ElementData>();
            ParameterData = ElementList.WorkshopParameters;
            ParameterValues = parameterValues;
        }

        public ElementList ElementList { get; private set; }
        public ElementData ElementData { get; private set; }
        public ParameterBase[] ParameterData { get; private set; }
        public string Name => ElementList.WorkshopName;

        public IWorkshopTree[] ParameterValues { get; set; }
        public bool Disabled { get; set; }
        public string Comment { get; set; }
        public int Indent { get; set; }
        protected bool AlwaysShowParentheses = false;

        public override string ToString()
        {
            return ElementList.GetLabel(false);
        }
        
        public virtual string ToWorkshop(OutputLanguage language, ToWorkshopContext context)
        {
            // Get the parameters
            AddMissingParameters();
            List<string> parameters = AdditionalParameters().ToList();
            parameters.AddRange(ParameterValues.Select(p => p.ToWorkshop(language, ToWorkshopContext.NestedValue)));

            string result = Extras.Indent(Indent, true); // TODO: option for spaces or tab output.

            // Add a comment and newline
            if (Comment != null) result += $"\"{Comment}\"\n" + Extras.Indent(Indent, true);

            // Add the disabled tag if the element is disabled.
            if (!ElementList.IsValue && Disabled) result += LanguageInfo.Translate(language, "disabled") + " ";

            // Add the name of the element.
            result += LanguageInfo.Translate(language, Name);

            // Add the parameters.
            if (parameters.Count != 0) result += "(" + string.Join(", ", parameters) + ")";
            else if (AlwaysShowParentheses) result += "()";

            // Add the ; if the element is an action.
            if (!ElementList.IsValue) result += ";";
            return result;
        }

        protected void AddMissingParameters()
        {
            List<IWorkshopTree> parameters = new List<IWorkshopTree>();

            for (int i = 0; i < ParameterData.Length || i < ParameterValues.Length; i++)
                parameters.Add(ParameterValues?.ElementAtOrDefault(i) ?? ParameterData[i].GetDefault());
            
            ParameterValues = parameters.ToArray();
        }

        public virtual bool ConstantSupported<T>()
        {
            return false;
        }
        public virtual object GetConstant()
        {
            return null;
        }

        public virtual Element Optimize()
        {
            OptimizeChildren();
            return this;
        }

        protected void OptimizeChildren()
        {
            AddMissingParameters();
            for (int i = 0; i < ParameterValues.Length; i++)
                if (ParameterValues[i] is Element)
                    ParameterValues[i] = ((Element)ParameterValues[i]).Optimize();
        }

        protected virtual string[] AdditionalParameters() => new string[0];

        // Creates an array from a list of values.
        public static Element CreateArray(params IWorkshopTree[] values)
        {
            if (values == null || values.Length == 0) return new V_EmptyArray();
            return Element.Part<V_Array>(values);
        }

        public static Element CreateAppendArray(params IWorkshopTree[] values)
        {
            Element array = new V_EmptyArray();
            for (int i = 0; i < values.Length; i++)
                array = Element.Part<V_Append>(array, values[i]);
            return array;
        }

        // Creates an ternary conditional that works in the workshop
        public static Element TernaryConditional(IWorkshopTree condition, IWorkshopTree consequent, IWorkshopTree alternative) => Element.Part<V_IfThenElse>(condition, consequent, alternative);

        public static Element operator +(Element a, Element b) => Element.Part<V_Add>(a, b);
        public static Element operator -(Element a, Element b) => Element.Part<V_Subtract>(a, b);
        public static Element operator *(Element a, Element b) => Element.Part<V_Multiply>(a, b);
        public static Element operator /(Element a, Element b) => Element.Part<V_Divide>(a, b);
        public static Element operator %(Element a, Element b) => Element.Part<V_Modulo>(a, b);
        public static Element operator <(Element a, Element b) => new V_Compare(a, Operators.LessThan, b);
        public static Element operator >(Element a, Element b) => new V_Compare(a, Operators.GreaterThan, b);
        public static Element operator <=(Element a, Element b) => new V_Compare(a, Operators.LessThanOrEqual, b);
        public static Element operator >=(Element a, Element b) => new V_Compare(a, Operators.GreaterThanOrEqual, b);
        public static Element operator !(Element a) => Element.Part<V_Not>(a);
        public static Element operator -(Element a) => a * -1;
        public Element this[Element i]
        {
            get { return Element.Part<V_ValueInArray>(this, i); }
            private set {}
        }
        public static implicit operator Element(double number) => new V_Number(number);
        public static implicit operator Element(int number) => new V_Number(number);
        public static implicit operator Element(bool boolean) => boolean ? (Element)new V_True() : new V_False();

        public bool EqualTo(IWorkshopTree b)
        {
            if (this.GetType() != b.GetType()) return false;

            Element bElement = (Element)b;
            if (ParameterValues.Length != bElement.ParameterValues.Length) return false;

            Type[] createsRandom = new Type[] {
                typeof(V_RandomInteger),
                typeof(V_RandomizedArray),
                typeof(V_RandomReal),
                typeof(V_RandomValueInArray)
            };

            for (int i = 0; i < ParameterValues.Length; i++)
            {
                if ((ParameterValues[i] == null) != (bElement.ParameterValues[i] == null))
                    return false;

                if (ParameterValues[i] != null && (!ParameterValues[i].EqualTo(bElement.ParameterValues[i]) || createsRandom.Contains(ParameterValues[i].GetType())))
                    return false;
            }
            
            return OverrideEquals(b);
        }

        protected virtual bool OverrideEquals(IWorkshopTree other) => true;

        public virtual int ElementCount()
        {
            AddMissingParameters();
            int count = 1;
            
            foreach (var parameter in ParameterValues)
                count += parameter.ElementCount();
            
            return count;
        }

        public Element OptimizeAddOperation(
            Func<double, double, double> op,
            Func<Element, Element, Element> areEqual,
            bool returnBIf0
        )
        {
            OptimizeChildren();

            Element a = (Element)ParameterValues[0];
            Element b = (Element)ParameterValues[1];

            V_Number aAsNumber = a as V_Number;
            V_Number bAsNumber = b as V_Number;

            // If a and b are numbers, operate them.
            if (aAsNumber != null && bAsNumber != null)
                return op(aAsNumber.Value, bAsNumber.Value);
            
            // If a is 0, return b.
            if (aAsNumber != null && aAsNumber.Value == 0 && returnBIf0)
                return b;
            
            // If b is 0, return a.
            if (bAsNumber != null && bAsNumber.Value == 0)
                return a;

            if (a.EqualTo(b))
                return areEqual(a, b);
            
            if (a.ConstantSupported<Vertex>() && b.ConstantSupported<Vertex>())
            {
                var aVertex = (Vertex)a.GetConstant();
                var bVertex = (Vertex)b.GetConstant();

                return new V_Vector(
                    op(aVertex.X, bVertex.X),
                    op(aVertex.Y, bVertex.Y),
                    op(aVertex.Z, bVertex.Z)
                );
            }
            
            return this;
        }

        public Element OptimizeMultiplyOperation(
            Func<double, double, double> op,
            Func<Element, Element, Element> areEqual,
            bool returnBIf1
        )
        {
            OptimizeChildren();

            Element a = (Element)ParameterValues[0];
            Element b = (Element)ParameterValues[1];

            V_Number aAsNumber = a as V_Number;
            V_Number bAsNumber = b as V_Number;

            // Multiply number and number
            if (aAsNumber != null && bAsNumber != null)
                return op(aAsNumber.Value, bAsNumber.Value);

            // Multiply vector and a vector
            if (a.ConstantSupported<Vertex>() && b.ConstantSupported<Vertex>())
            {
                Vertex vertexA = (Vertex)a.GetConstant();
                Vertex vertexB = (Vertex)b.GetConstant();
                return new V_Vector(
                    op(vertexA.X, vertexB.X),
                    op(vertexA.Y, vertexB.Y),
                    op(vertexA.Z, vertexB.Z)
                );
            }

            // Multiply vector and number
            if ((a.ConstantSupported<Vertex>() && b is V_Number) || (a is V_Number && b.ConstantSupported<Vertex>()))
            {
                Vertex vector = a.ConstantSupported<Vertex>() ? (Vertex)a.GetConstant() : (Vertex)b.GetConstant();
                V_Number number = a is V_Number ? (V_Number)a : (V_Number)b;
                return new V_Vector(
                    op(vector.X, number.Value),
                    op(vector.Y, number.Value),
                    op(vector.Z, number.Value)
                );
            }

            if (aAsNumber != null)
            {
                if (aAsNumber.Value == 1 && returnBIf1) return b;
                if (aAsNumber.Value == 0) return 0;
            }

            if (bAsNumber != null)
            {
                if (bAsNumber.Value == 1) return a;
                if (bAsNumber.Value == 0) return 0;
            }

            if (a.EqualTo(b))
                return areEqual(a, b);
            
            return this;
        }
    }

    public class ElementList : IMethod
    {
        public static ElementList[] Elements { get; private set; }

        public static void InitElements()
        {
            Type[] methodList = Assembly.GetExecutingAssembly().GetTypes().Where(t => t.GetCustomAttribute<ElementData>() != null).ToArray();

            Elements = new ElementList[methodList.Length];
            for (int i = 0; i < Elements.Length; i++)
                Elements[i] = new ElementList(methodList[i]);

            for (int i = 0; i < Elements.Length; i++)
                Elements[i].ApplyParameters();
        }
        public static ElementList GetElement(string codeName)
        {
            return Elements.FirstOrDefault(e => e.Name == codeName);
        }
        public static ElementList GetElement<T>() where T: Element
        {
            return Elements.FirstOrDefault(e => e.Type == typeof(T));
        }
        public static ElementList FromType(Type type)
        {
            return Elements.FirstOrDefault(element => element.Type == type);
        }

        public string Name { get; }
        public string WorkshopName { get; }
        public Type Type { get; }
        public bool IsValue { get; } 
        public bool Hidden { get; }
        public CodeParameter[] Parameters { get; private set; }
        public ParameterBase[] WorkshopParameters { get; }
        public MethodAttributes Attributes { get; } = new MethodAttributes();
        public UsageDiagnostic[] UsageDiagnostics { get; }
        public WikiMethod Wiki { get; }
        public string Documentation => Wiki?.Description;
        public ValueType ElementValueType { get; }
        private RestrictedCallType? Restricted { get; }

        // IScopeable defaults
        public LanguageServer.Location DefinedAt { get; } = null;
        public AccessLevel AccessLevel { get; } = AccessLevel.Public;
        public bool WholeContext { get; } = true;
        public bool Static => true;

        public CodeType ReturnType { get; private set; }

        public ElementList(Type type)
        {
            ElementData data = type.GetCustomAttribute<ElementData>();
            Name = type.Name.Substring(2); 
            WorkshopName = data.ElementName;
            Type = type;
            IsValue = data.IsValue;
            ElementValueType = data.ValueType;
            WorkshopParameters = type.GetCustomAttributes<ParameterBase>().ToArray();
            UsageDiagnostics = type.GetCustomAttributes<UsageDiagnostic>().ToArray();
            Hidden = type.GetCustomAttribute<HideElement>() != null;
            Restricted = type.GetCustomAttribute<RestrictedAttribute>()?.Type;

            Wiki = WorkshopWiki.Wiki.GetWiki()?.GetMethod(WorkshopName);
        }

        public void ApplyParameters()
        {
            // Set the return type to the Vector class if the value returns a vector.
            if (ElementValueType == ValueType.Vector) ReturnType = VectorType.Instance;

            // Get the parameters.
            Parameters = new Parse.CodeParameter[WorkshopParameters.Length];
            for (int i = 0; i < Parameters.Length; i++)
            {
                string name = WorkshopParameters[i].Name.Replace(" ", "");
                string description = Wiki?.GetWikiParameter(WorkshopParameters[i].Name)?.Description;

                if (WorkshopParameters[i] is VarRefParameter)
                {
                    Parameters[i] = new VariableParameter(
                        name,
                        description,
                        ((VarRefParameter)WorkshopParameters[i]).IsGlobal ? VariableType.Global : VariableType.Player,
                        new VariableResolveOptions() {
                            CanBeIndexed = false, FullVariable = true
                        }
                    );
                }
                else
                {
                    CodeType codeType = null;

                    // If the parameter is an enum, get the enum CodeType.
                    if (WorkshopParameters[i] is EnumParameter)
                        codeType = ValueGroupType.GetEnumType(((EnumParameter)WorkshopParameters[i]).EnumData);

                    var defaultValue = WorkshopParameters[i].GetDefault();

                    Parameters[i] = new CodeParameter(
                        name,
                        description,
                        codeType,
                        defaultValue == null ? null : new ExpressionOrWorkshopValue(defaultValue)
                    );

                    // If the default parameter value is an Element and the Element is restricted,
                    if (defaultValue is Element parameterElement && parameterElement.ElementList.Restricted != null)
                        // ...then add the restricted call type to the parameter's list of restricted call types.
                        Parameters[i].RestrictedCalls.Add((RestrictedCallType)parameterElement.ElementList.Restricted);
                }
            }
        }

        public Element GetObject()
        {
            return (Element)Activator.CreateInstance(Type);
        }

        public bool DoesReturnValue => IsValue;

        public IWorkshopTree Parse(ActionSet actionSet, MethodCall methodCall)
        {
            Element element = GetObject();
            element.ParameterValues = methodCall.ParameterValues;
            element.Comment = methodCall.ActionComment;

            if (!IsValue)
            {
                actionSet.AddAction(element);
                return null;
            }
            else return element;
        }

        public string GetLabel(bool markdown) => HoverHandler.GetLabel(!IsValue ? null : ReturnType?.Name ?? "define", Name, Parameters, markdown, Wiki?.Description);

        public CompletionItem GetCompletion() => MethodAttributes.GetFunctionCompletion(this);

        public void Call(ParseInfo parseInfo, DocRange callRange)
        {
            if (Restricted != null)
                // If there is a restricted call type, add it.
                parseInfo.RestrictedCallHandler.RestrictedCall(new RestrictedCall(
                    (RestrictedCallType)Restricted,
                    parseInfo.GetLocation(callRange),
                    RestrictedCall.Message_Element((RestrictedCallType)Restricted)
                ));
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class UsageDiagnostic : Attribute
    {
        public UsageDiagnostic(string message, int severity)
        {
            Message = message;
            Severity = severity;
        }

        public string Message { get; }
        public int Severity { get; }

        public LanguageServer.Diagnostic GetDiagnostic(DocRange range)
        {
            return new LanguageServer.Diagnostic(Message, range, Severity);
        }
    }
}