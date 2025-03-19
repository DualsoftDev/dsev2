global using System;
global using System.Data;
global using System.IO;
global using System.Linq;
global using System.Windows.Forms;
global using System.Text.RegularExpressions;
global using System.Collections.Generic;
global using System.ComponentModel;

global using Microsoft.FSharp.Core;
global using Newtonsoft.Json;

global using DevExpress.XtraGrid.Views.Grid;
global using DevExpress.XtraEditors.Repository;
global using DevExpress.XtraGrid.Columns;
global using DevExpress.Utils.Extensions;

global using Dual.Common.Winform.DevX;
global using Dual.Common.Core;
global using Dual.Common.Base.CS;
global using Dual.Common.Winform;
global using Dual.Common.Base.FS;
global using Dual.Plc2DS;

global using LS = Dual.Plc2DS.LS;
global using AB = Dual.Plc2DS.AB;
global using S7 = Dual.Plc2DS.S7;
global using MX = Dual.Plc2DS.MX;

global using static Dual.Plc2DS.AppSettingsModule;
global using Plc2DsApp.Forms;
global using static GridExtension;