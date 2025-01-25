using System.Reflection;
using System.Runtime.Versioning;


#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif

[assembly: AssemblyCompany("Dualsoft")]
[assembly: AssemblyCopyright("Copyright © Dualsoft 2016")]
[assembly: AssemblyTitle("Dualsoft Engine packages")]
[assembly: AssemblyProduct("DS Engine")]

///DS Library Version (Library File 수정시만 변경 Ver : 년.월.시)
[assembly: AssemblyDescription("Library Release Date: 2024-03-26")]
///DS Language Version (Language Parser 수정시만 변경 Ver : 1.0.0.1)
[assembly: AssemblyInformationalVersion("1.0.0.1")]
///DS Engine Version
[assembly: AssemblyFileVersion("0.9.10.32")]
