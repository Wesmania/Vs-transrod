make:
	ls $(VS)/*.dll $(VS)/Lib/*.dll $(DOTNET)/*.dll | sed 's@^@/reference:@' | xargs csc main.cs /out:transrod.dll -t:library
	zip transrod.zip transrod.dll modinfo.json transrod.deps.json

clean:
	rm transrod.zip
	rm transrod.dll
