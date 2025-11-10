using PLC.Convert.LSCore.XGTTag;
using System;
using System.Linq;

namespace PLC.Convert.LSCore
{
    /// <summary>
    /// Represents a symbol in the program.
    /// </summary>
    public class Symbol
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Symbol"/> class with the specified name and optional initial value.
        /// </summary>
        /// <param name="name">The name of the symbol.</param>
        /// <param name="value">The initial value of the symbol (default is false).</param>
        public Symbol(string taskName, string name)
        {
            TaskName = taskName;
            Name = name;
        }
        //for unitTest
        public Symbol(string name, bool v) { Name = name; Value = v; }
        //for unitTest
        public Symbol(string name) { Name = name; }

        /// <summary>
        /// Gets the name of the symbol.
        /// </summary>
        public string Name { get; private set; }
        /// <summary>
        /// Gets the taskName of the symbol.
        /// </summary>
        public string TaskName { get; private set; }
        /// <summary>
        /// Gets the Key Name of the symbol.
        /// </summary>
        public string KeyName { get; private set; }
        /// <summary>
        /// Gets or sets the global type of the symbol.
        /// </summary>
        public bool IsGlobal { get; set; }
        /// <summary>
        /// Gets or sets the description of the symbol.
        /// </summary>
        public string Comment { get; set; }

        /// <summary>
        /// Gets or sets the device associated with the symbol.
        /// </summary>
        public string Device { get; set; }

        /// <summary>
        /// Gets or sets the start offset of the symbol in bits.
        /// </summary>
        public int BitStartOffset { get; set; }

        /// <summary>
        /// Gets or sets the end offset of the symbol in bits.
        /// </summary>
        public int BitEndOffset { get; set; }

        /// <summary>
        /// Gets or sets the size of the symbol in bits.
        /// </summary>
        public int BitSize { get; set; }

        /// <summary>
        /// Gets or sets the byte offset of the symbol.
        /// </summary>
        public string ByteOffset { get; set; }

        /// <summary>
        /// Gets the address of the symbol, which is based on its type and offset.
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// Gets or sets the kind of variable represented by the symbol.
        /// </summary>
        public string VarKind { get; set; }

        /// <summary>
        /// Gets or sets the data SizeType of the symbol.
        /// </summary>
        public SymbolDataType SymbolDataType { get; set; }
        public string SymbolDataTypeDisplayText => CallParaUtil.GetSymbolDataTypeText(SymbolDataType); 
        public ISymbolArray SymbolArray { get; set; }
        public bool IsArrayMember => Name.Contains('[') && Name.Contains(']');
        public string UserDataName { get; set; }

     

        /// <summary>
        /// Gets or sets the value of the symbol.
        /// </summary>
        public bool Value { get; set; }
      

        /// <summary>
        /// Returns the name of the symbol.
        /// </summary>
        /// <returns>The name of the symbol.</returns>
        public override string ToString() { return Name; }

        public XGTDevice XGTDevice { get;  set; }
      
        public void UpdateSymbolFromXGTDevice()
        {
            if (XGTDevice == null) { throw new Exception($"need CreateXGTDevice"); }
            if (XGTDevice is XGTDeviceBit xgtBit)
                this.Value = xgtBit.Value;
            else
                this.Value = XGTDevice.ToTextValue() != "0"; //0 아니면 true 처리
        }

        public void UpdateKeyName(SymbolDataType sdt, bool isGlobal)
        {
            KeyName = SymbolUtil.GetKey(TaskName, Name, sdt, isGlobal);
        }
        public void UpdateParameter(SymbolDataType sdt, bool isGlobal, string address, string device, int bitStart, int bitEnd)
        {
            Address = address;
            Device = device;
            BitStartOffset = bitStart;
            BitEndOffset   = bitEnd;
            SymbolDataType = sdt;
            UpdateKeyName(sdt, isGlobal);
        }
    }
}
