namespace PLC.Convert.LSCore
{
    /// <summary>
    /// Represents the parameters for a function call.
    /// </summary>
    public class CallPara
    {
        /// <summary>
        /// Gets or sets the MainProgram name associated with the call parameter.
        /// </summary>
        public string MainProgram { get; set; }
        /// <summary>
        /// Gets or sets the ConfigName name associated with the call parameter.
        /// </summary>
        public string ConfigName { get; set; }

        /// <summary>
        /// Gets or sets the tag name associated with the call parameter.
        /// </summary>
        public string TagName { get; set; }
        /// <summary>
        /// Gets or sets the Address associated with the call parameter.
        /// </summary>
        public string Address { get; set; }
        public bool IsXgi { get; set; }
        public bool IsGlobal { get; set; }
        public int Line { get; set; }
        
        /// <summary>
        /// Gets or sets the X point associated with the call parameter.
        /// </summary>
        public int XPoint { get; set; }

        /// <summary>
        /// Gets or sets the Y point associated with the call parameter.
        /// </summary>
        public int YPoint { get; set; }

    
        /// <summary>
        /// Gets or sets the SymbolDataType  associated with the call parameter.
        /// </summary>
        public SymbolDataType SymbolDataType { get; set; }

        /// <summary>
        /// Gets or sets the Comment  associated with the call parameter.
        /// </summary>
        public string Comment { get; set; }
        public bool IsAssigned { get; set; }


        /// <summary>
        /// Initializes a new instance of the <see cref="CallPara"/> class with the specified parameters.
        /// </summary>
        /// <param name="mainProgram">The mainProgram name.</param>
        /// <param name="tagName">The tag name.</param>
        /// <param name="xPoint">The X point.</param>
        /// <param name="yPoint">The Y point.</param>
        /// <param name="scanName">The scan name.</param>
        /// <param name="rungNum">The rung number.</param>
        /// <param name="configName">The configName name.</param>
        public CallPara(string mainProgram,  
                        string tagName, 
                        string address, 
                        int line,
                        int xPoint, int yPoint, 
                        string configName,
                        string comment, 
                        SymbolDataType symbolDataType, 
                        bool isXgi,
                        bool isGlobal)
        {
            MainProgram = mainProgram;
            ConfigName = configName;
            TagName = tagName;
            Address = address;
            Line = line;
            XPoint = xPoint;
            YPoint = yPoint;
            Comment = comment;
            SymbolDataType = symbolDataType;
            IsXgi = isXgi;
            IsGlobal = isGlobal;
        }
    }
}
