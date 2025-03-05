
default:
	make gui file=datatype.l5k target=struct_def

file ?= "min.l5k"
target ?= "l5kFile"
gui:
	@echo "You typed ${file}"
	antlr4-parse.exe l5k.g4 ${target} < ${file}  -tree -gui

