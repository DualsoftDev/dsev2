md2adoc:
	pandoc IDTA_SequenceControl_Submodel_Template.md -f markdown -t asciidoc -o SequenceControl.adoc

copyFont:
	mkdir -p fonts
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


install:
	download_and_install.bat https://github.com/oneclick/rubyinstaller2/releases/download/RubyInstaller-3.4.5-1/rubyinstaller-devkit-3.4.5-1-x64.exe
	gem install asciidoctor
	gem install asciidoctor-pdf --pre
