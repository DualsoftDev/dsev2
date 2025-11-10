
default:
	make gui file=connection.min.l5k target=connection
	make gui file=module.min.l5k target=module
	make gui file=datatype.l5k target=struct_def
	make gui file=min.l5k target=l5kFile
	make tokens file=datatype.l5k target=struct_def

file ?= "min.l5k"
target ?= "l5kFile"
gui:
	@echo "You typed ${file}"
	antlr4-parse.exe l5k.g4 ${target} < ${file}  -tree -gui

tokens:
	antlr4-parse l5k.g4 ${target} -tokens < ${file}
