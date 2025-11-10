using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;

namespace PLC.Convert.LSCore.Expression
{
    /// <summary>
    /// Represents an abstract expression.
    /// </summary>
    public abstract class Expr
    {
        /// <summary>
        /// Evaluates the expression.
        /// </summary>
        /// <returns>Result of the evaluation.</returns>
        public abstract bool Evaluate();

        /// <summary>
        /// Converts the expression to text.
        /// </summary>
        /// <returns>Text representation of the expression.</returns>
        public abstract string ToText();

        /// <summary>
        /// Gets the terminals used in the expression.
        /// </summary>
        /// <returns>Enumeration of terminals.</returns>
        public abstract IEnumerable<Terminal> GetTerminals();

        /// <summary>
        /// Analyzes the reason for the expression's value.
        /// </summary>
        /// <param name="whyTrue">True if analyzing why the expression is true, false otherwise.</param>
        /// <returns>Enumeration of terminals contributing to the expression's value.</returns>
        public abstract void AnalyzeReason(bool whyTrue, List<Terminal> reasons);
        public abstract void AnalyzeCausal(List<Terminal> reasons);
    }

    public enum TerminalType
    {
        COIL,
        CONTACT,
        FB_OUT,
        FB_IN,
    }

    /// <summary>
    /// Represents a terminal expression.
    /// </summary>
    [DebuggerDisplay("{Name}")]
    public class Terminal : Expr
    {
        /// <summary>
        /// Gets the call parameter associated with the terminal.
        /// </summary>
        [Browsable(false)]
        public CallPara CallPara { get; private set; }

        /// <summary>
        /// Gets or sets the inner expression if this terminal is a coil.
        /// </summary>
        [Browsable(false)]
        public Expr InnerExpr { get; set; }

        
        /// <summary>
        /// Gets a value indicating whether this terminal is a coil.
        /// </summary>
        public TerminalType Type { get; set; } = TerminalType.CONTACT;
        [Browsable(false)]
        public bool IsOutputType => Type is TerminalType.COIL or TerminalType.FB_OUT;
        [Browsable(false)]
        public bool HasInnerExpr => InnerExpr != null;
     

        /// <summary>
        /// Gets the symbol associated with the terminal.
        /// </summary>
        [Browsable(false)]
        public Symbol Symbol { get; }

        /// <summary>
        /// Gets the name of the terminal.
        /// </summary>
        public string Name { get { return Symbol.Name; } }
        [Browsable(false)]
        public SymbolDataType DataType { get { return CallPara.SymbolDataType; } }
        public string DeviceType => CallParaUtil.GetSymbolDataTypeText(CallPara.SymbolDataType);
        public string Task { get { return CallPara.MainProgram; } }
        public string Point { get { return $"Line:{CallPara.Line}:(X:{CallPara.XPoint}, Y:{CallPara.YPoint})"; } }

        //[Browsable(false)]
        //public string NameSkipTask => Symbol.IsGlobal ? Symbol.Name 
        //                              : String.Join("|", Symbol.Name.Split('|').Skip(1));

        /// <summary>
        /// Gets or sets a value indicating whether the terminal has been evaluated.
        /// </summary>
        [Browsable(false)]
        public bool Evaluated { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether an error occurred during evaluation.
        /// </summary>
        [Browsable(false)]
        public bool Error { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the terminal is selected.
        /// </summary>
        [Browsable(false)]
        public bool Selected { get; set; }
        /// <summary>
        /// Returns the name of the symbol.
        /// </summary>
        /// <returns>The name of the symbol.</returns>
        public override string ToString() { return Name; }
        /// <summary>
        /// Initializes a new instance of the <see cref="Terminal"/> class.
        /// </summary>
        /// <param name="symbol">The symbol associated with the terminal.</param>
        /// <param name="callPara">The call parameter associated with the terminal.</param>
        public Terminal(Symbol symbol, CallPara callPara)
        {
            Symbol = symbol;
            CallPara = callPara;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Terminal"/> class for unit testing.
        /// </summary>
        /// <param name="symbol">The symbol associated with the terminal.</param>
        public Terminal(Symbol symbol)
        {
            Symbol = symbol;
            CallPara = new CallPara("", symbol.Name, symbol.Address, 0, 0, 0, "", "", SymbolDataType.NONE, false, false);   
        }

        /// <inheritdoc/>
        public override IEnumerable<Terminal> GetTerminals()
        {
            switch (Type)
            {
                case TerminalType.COIL:
                case TerminalType.FB_OUT:
                    if (HasInnerExpr && InnerExpr is not TerminalDummy)
                        return InnerExpr.GetTerminals();
                    else
                        return new[] { this };
                case TerminalType.CONTACT:
                case TerminalType.FB_IN: return new[] { this };
                default: throw new Exception($"TerminalType error : {Type}");
            }
        }

        /// <inheritdoc/>
        public override bool Evaluate()
        {
            switch (Type)
            {
                case TerminalType.COIL: return InnerExpr.Evaluate();
                case TerminalType.CONTACT: return Symbol.Value;
                case TerminalType.FB_OUT:
                case TerminalType.FB_IN: return true;
                default: throw new Exception($"TerminalType error : {Type}");
            }
        }

        /// <inheritdoc/>
        public override string ToText()
        {
            return Symbol.Name;
        }


        public override void AnalyzeReason(bool whyTrue, List<Terminal> reasons)
        {
            if ((Symbol.Value == whyTrue && !reasons.Contains(this)) 
              || Type is TerminalType.FB_IN or TerminalType.FB_OUT 
              || Symbol.IsArrayMember)
            {
                reasons.Add(this);

                if (HasInnerExpr)
                    InnerExpr.AnalyzeReason(whyTrue, reasons);
            }
        }

        public override void AnalyzeCausal(List<Terminal> reasons)
        {
            if (!reasons.Contains(this))
            {
                reasons.Add(this);
                if (HasInnerExpr)
                    InnerExpr.AnalyzeCausal(reasons);
            }
        }

    }

    /// <summary>
    /// Represents a dummy terminal for special cases.
    /// </summary>
    public class TerminalDummy : Expr
    {
        /// <summary>
        /// Gets or sets the inner expression if this terminal is a coil.
        /// </summary>
        public Expr InnerExpr { get; set; }

        /// <summary>
        /// Gets or sets the name of the terminal.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TerminalDummy"/> class.
        /// </summary>
        /// <param name="name">The name of the terminal.</param>
        public TerminalDummy(string name)
        {
            Name = name;
        }

        /// <inheritdoc/>
        public override IEnumerable<Terminal> GetTerminals()
        {
            return Enumerable.Empty<Terminal>();
        }

        /// <inheritdoc/>
        public override bool Evaluate()
        {
            if (InnerExpr != null)
                return InnerExpr.Evaluate();
            else  
                return false;
        }

        /// <inheritdoc/>
        public override string ToText()
        {
            return Name;
        }


        public override void AnalyzeReason(bool whyTrue, List<Terminal> reasons)
        {
        }
        public override void AnalyzeCausal(List<Terminal> reasons)
        {
        }
    }

    /// <summary>
    /// Represents an AND logical operation.
    /// </summary>
    public class And : Expr
    {
        private readonly Expr _left;
        private readonly Expr _right;

        /// <summary>
        /// Initializes a new instance of the <see cref="And"/> class.
        /// </summary>
        /// <param name="left">The left operand of the AND operation.</param>
        /// <param name="right">The right operand of the AND operation.</param>
        public And(Expr left, Expr right)
        {
            _left = left ?? throw new ArgumentNullException(nameof(left));
            _right = right ?? throw new ArgumentNullException(nameof(right));
        }

        /// <inheritdoc/>
        public override IEnumerable<Terminal> GetTerminals()
        {
            return _left.GetTerminals().Concat(_right.GetTerminals());
        }

        /// <inheritdoc/>
        public override bool Evaluate()
        {
            return _left.Evaluate() && _right.Evaluate();
        }

        /// <inheritdoc/>
        public override string ToText()
        {
            return $"({_left.ToText()} & {_right.ToText()})";
        }

      
        public override void AnalyzeReason(bool whyTrue, List<Terminal> reasons)
        {
            bool leftValue = _left.Evaluate();
            bool rightValue = _right.Evaluate();

            if (whyTrue)
            {
                // Both expressions must be true for the AND operation to be true.
                if( leftValue && rightValue)
                {
                    _left.AnalyzeReason(true, reasons);
                    _right.AnalyzeReason(true, reasons);
                }
            }
            else
            {
                if (!leftValue) _left.AnalyzeReason(false, reasons);
                if (!rightValue) _right.AnalyzeReason(false, reasons);
            }
        }

        public override void AnalyzeCausal(List<Terminal> reasons)
        {
            _left.AnalyzeCausal(reasons);
            _right.AnalyzeCausal(reasons);
        }
    }

    /// <summary>
    /// Represents an OR logical operation.
    /// </summary>
    public class Or : Expr
    {
        private readonly Expr _left;
        private readonly Expr _right;

        /// <summary>
        /// Initializes a new instance of the <see cref="Or"/> class.
        /// </summary>
        /// <param name="left">The left operand of the OR operation.</param>
        /// <param name="right">The right operand of the OR operation.</param>
        public Or(Expr left, Expr right)
        {
            _left = left ?? throw new ArgumentNullException(nameof(left));
            _right = right ?? throw new ArgumentNullException(nameof(right));
        }

        /// <inheritdoc/>
        public override IEnumerable<Terminal> GetTerminals()
        {
            return _left.GetTerminals().Concat(_right.GetTerminals());
        }

        /// <inheritdoc/>
        public override bool Evaluate()
        {
            return _left.Evaluate() || _right.Evaluate();
        }

        /// <inheritdoc/>
        public override string ToText()
        {
            return $"({_left.ToText()} | {_right.ToText()})";
        }

        /// <inheritdoc/>
        public override void AnalyzeReason(bool whyTrue, List<Terminal> reasons)
        {
            bool leftValue = _left.Evaluate();
            bool rightValue = _right.Evaluate();

            if (whyTrue)
            {
                if (leftValue) _left.AnalyzeReason(true, reasons);
                if (rightValue) _right.AnalyzeReason(true, reasons);
            }
            else
            {
                if (!leftValue && !rightValue)
                {
                    _left.AnalyzeReason(false, reasons);
                    _right.AnalyzeReason(false, reasons);
                }
            }
        }
        public override void AnalyzeCausal(List<Terminal> reasons)
        {
            _left.AnalyzeCausal(reasons);
            _right.AnalyzeCausal(reasons);
        }

    }

    /// <summary>
    /// Represents a NOT logical operation.
    /// </summary>
    public class Not : Expr
    {
        private readonly Expr _expr;

        /// <summary>
        /// Initializes a new instance of the <see cref="Not"/> class.
        /// </summary>
        /// <param name="expr">The expression to negate.</param>
        public Not(Expr expr)
        {
            _expr = expr ?? throw new ArgumentNullException(nameof(expr));
        }

        /// <inheritdoc/>
        public override IEnumerable<Terminal> GetTerminals()
        {
            return _expr.GetTerminals();
        }

        /// <inheritdoc/>
        public override bool Evaluate()
        {
            return !_expr.Evaluate();
        }

        /// <inheritdoc/>
        public override string ToText()
        {
            return $"!{_expr.ToText()}";
        }


        /// <inheritdoc/>
     
        public override void AnalyzeReason(bool whyTrue, List<Terminal> reasons)
        {
            _expr.AnalyzeReason(!whyTrue, reasons);
        }
        public override void AnalyzeCausal(List<Terminal> reasons)
        {
            _expr.AnalyzeCausal(reasons);
        }
    }
}
