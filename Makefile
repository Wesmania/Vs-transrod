make:
	ls $(VS)/*.dll $(VS)/Lib/*.dll $(VS)/Mods/VSSurvivalMod.dll $(VS)/Mods/VSEssentials.dll $(DOTNET)/*.dll | sed 's@^@/reference:@' | xargs csc main.cs /out:transrod.dll -t:library
	zip -r transrod.zip assets transrod.dll modinfo.json transrod.deps.json

clean:
	rm transrod.zip
	rm transrod.dll
