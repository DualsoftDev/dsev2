md2adoc:
	pandoc IDTA_SequenceControl_Submodel_Template.md -f markdown -t asciidoc -o SequenceControl.adoc

pdf:
	magick -density 300 cover.pdf cover.png
	asciidoctor-pdf -a pdf-theme=custom-theme.yml -a pdf-themesdir=. SequenceControl.adoc
