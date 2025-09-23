diet:
	@rm -rf misc spec-test BuildAll docs scripts

	@( \
		cd src; \
		rm -rf DraftVer1 spec-test misc; \
	)

	@( \
		cd submodules/nuget; \
		rm -rf All.Nuget.Loader Others PLC Tests UnitTest.Nuget.Common.Net48 Web Windows; \
		cd Common; \
		rm -rf Dual.Common{Akka, Antlr.FS, Base.CS, Core, Db, DevExpressLib, Drawing, FSharpInterop.FS, IonicZip, Utils, Obsolete}; \
	)


# submodule 이 없을 때는 동작 함
undiet_simple:
	@git ls-files --deleted -z | xargs -0 git restore


undiet:
	@echo "Restoring deleted files in root repo..."
	@if [ -n "$$(git ls-files --deleted)" ]; then \
		git ls-files --deleted -z | xargs -0 git restore; \
	else \
		echo "  Nothing to restore in root."; \
	fi

	@echo "Restoring in submodules..."
	@for sub in submodules/nuget src; do \
		if [ -d $$sub ]; then \
			echo "  - $$sub"; \
			cd $$sub; \
			if [ -n "$$(git ls-files --deleted)" ]; then \
				git ls-files --deleted -z | xargs -0 git restore; \
			else \
				echo "    Nothing to restore in $$sub."; \
			fi; \
			cd - > /dev/null; \
		fi; \
	done


clean: test-clean
	@find . -type d \( -name "bin" -o -name "obj" -o -name ".vs" \) -exec rm -rf {} +

test-clean:
	rm -rf ./src/unit-test/UnitTest.Core/test-data
	(cd ./scripts; ./initialize-pgsql-schema.sh -u dstest)
index:
	CodeIndexer.sh index dsev2.sln
