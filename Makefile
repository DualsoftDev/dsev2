diet:
	rm -rf src/DraftVer1 misc spec-test
	(
		cd submodules/nuget;
		rm -rf All.Nuget.Loader Others PLC Tests UnitTest.Nuget.Common.Net48 Web Windows
		cd Common
		rm -rf Dual.Common{Akka, Antlr.FS, Base.CS, Core, Db, DevExpressLib, Drawing, FSharpInterop.FS, IonicZip, Utils, Obsolete}
	)