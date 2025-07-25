md2adoc:
	pandoc IDTA_SequenceControl_Submodel_Template.md -f markdown -t asciidoc -o SequenceControl.adoc

copyFont:
	cp /c/Windows/Fonts/NotoSansKR-VF.ttf ./fonts/NotoSansKR-VF.ttf

FONTS_DIR_RAW := $(shell pwd)
FONTS_DIR := $(subst \,/,$(FONTS_DIR_RAW))
pdf:
#magick -density 300 cover.pdf cover.png
#asciidoctor-pdf -a pdf-theme=custom-theme.yml -a pdf-themesdir=. SequenceControl.adoc
#	asciidoctor-pdf -a pdf-theme=custom-theme.yml -a pdf-themesdir=. -a pdf-fontsdir=fonts SequenceControl.adoc

	asciidoctor-pdf -a pdf-theme=custom-theme.yml \
	-a pdf-themesdir=. \
	-a pdf-fontsdir="$(FONTS_DIR)/fonts" \
	-a chapter-label="" \
	-a chapter-refsig="" \
	-a section-refsig="" \
	-a chapter-signifier="" \
	-a untitled-label="" \
	SequenceControl.adoc