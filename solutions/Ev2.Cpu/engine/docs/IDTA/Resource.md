https://industrialdigitaltwin.org/en/content-hub/submodels
https://industrialdigitaltwin.org/en/wp-content/uploads/sites/2/2025/03/IDTA-02063-1-0_Submodel__IntelligentInformationForUse.pdf



편집 툴
https://github.com/admin-shell-io/idta-submodel-templates?tab=readme-ov-file
    https://antora.org/


https://github.com/admin-shell-io/idta-submodel-templates/tree/main?tab=readme-ov-file

루비 설치
https://github.com/oneclick/rubyinstaller2/releases/download/RubyInstaller-3.4.4-2/rubyinstaller-devkit-3.4.4-2-x64.exe

C:\Users\dualk>gem -v
3.6.7

C:\Users\dualk>gem install asciidoctor
Successfully installed asciidoctor-2.0.23
1 gem installed



npm install -g @antora/cli @antora/site-generator-default
antora --version


$ npm install -g @antora/cli @antora/site-generator-default
npm warn deprecated inflight@1.0.6: This module is not supported, and leaks memory. Do not use it. Check out lru-cache if you want a good and tested way to coalesce async requests by a key value, which is much more comprehensive and powerful.
npm warn deprecated glob@7.1.3: Glob versions prior to v9 are no longer supported

added 214 packages in 1s

29 packages are looking for funding
  run `npm fund` for details
|/f/Git/IDTA/idta-submodel-templates|$ antora --version
@antora/cli: 3.1.10
@antora/site-generator: not installed

### pandoc
https://github.com/jgm/pandoc/releases/download/3.7.0.2/pandoc-3.7.0.2-windows-x86_64.msi

### imagemagic
https://imagemagick.org/archive/binaries/ImageMagick-7.1.2-0-Q16-HDRI-x64-dll.exe
    - pdf -> png 이미지 변환기
    - https://github.com/ArtifexSoftware/ghostpdl-downloads/releases/download/gs10051/gs10051w64.exe 설치 필요

IDTA에서 제공하는 **AAS Submodel Template(이하 SMT)**는 주로 PDF 또는 AsciiDoc 형식으로 제공되며, Microsoft Word(.docx) 형식은 공식적으로 제공되지 않습니다. 하지만 편집이 가능한 형태로 SMT를 활용하려면 다음과 같은 방법들이 있습니다:
🔧 1. 공식 GitHub 리포지토리 활용

IDTA는 SMT의 AsciiDoc 소스 파일과 자동 생성된 PDF/HTML을 포함하는 GitHub 리포지토리를 운영 중입니다:

    리포지토리 주소: admin-shell-io/submodel-templates
    https://www.hannovermesse.de+15GitHub+15idtaportal.admin-shell-io.com+15
    industrialdigitaltwin.io+7GitHub+7GitHub+7

AsciiDoc 소스는 일반 텍스트(.adoc) 형태이므로, 이를 자유롭게 편집하거나 DOCX로 변환하기 좋습니다.
✍️ 2. AsciiDoc → Word(.docx) 변환

AsciiDoc 파일을 다음과 같은 방법으로 DOCX로 변환할 수 있습니다:

asciidoctor-pdf input.adoc -o temp.pdf
pandoc temp.pdf -o output.docx

    또는 AsciiDoc → Markdown → DOCX 순 변환도 가능합니다.

    이 방법으로 "편집 가능한 Word 템플릿"을 사실상 제작할 수 있습니다.

📄 3. PDF → Word 변환

IDTA 공홈에서 다운로드 가능한 SMT는 PDF 형식입니다:

    예: "Create a Submodel Template Specification", "Registration of AAS Submodel Templates" 등
    industrialdigitaltwin.org+2industrialdigitaltwin.org+2industrialdigitaltwin.org+2

PDF를 Word로 변환하는 경우, 편집 가능하지만 텍스트 깨짐, 서식 손실 등이 있을 수 있다는 점 참고하세요.
✅ 예시: AsciiDoc 활용 절차

    IDTA GitHub 리포지토리에서 원하는 SMT 폴더(예: 02007-1-0 Nameplate for Software)의 .adoc 파일 다운로드

    AsciiDoc 편집 도구 또는 텍스트 편집기로 수정

    문서 변환 도구(Asciidoctor, pandoc 등)를 이용해 PDF/DOCX 등 원하는 형식으로 출력

🧭 요약
항목	제공여부	비고
AsciiDoc (.adoc)	✅	GitHub에서 다운로드, 편집 가능
PDF	✅	공식 웹사이트에서 다운로드
DOCX	❌	공식 제공 없음, 변환 필요
🎯 필요한 템플릿 바로 시작하기

    SMT AsciiDoc 소스 확보

        GitHub 리포지토리 접근 후 원하는 SMT 폴더에서 .adoc 파일 다운로드

        예: “Digital Nameplate”이나 “Software Nameplate”

    변환 도구 활용

        AsciiDoc을 수정 후 pandoc 등으로 DOCX 변환

    PDF → Word

        공식 PDF 문서는 편집툴(예: Adobe Acrobat DC, 온라인 변환기)로 Word 형식으로 변환 가능        1  cd dsev2/

   57  asciidoctor nav.adoc 
   75  git clone https://github.com/admin-shell-io/idta-submodel-templates.git
   79  find . -name '*.adoc'
   97  npm install -g @antora/cli @antora/site-generator-default
   98  antora --version
   99  antora antora-playbook.yml

  106  npm init -y
  107  npm install @antora/cli @antora/site-generator-default
  108  npx antora antora-playbook.yml


  109  gem install bundler
  112  npx antora antora-playbook.yml
  113  dir Gemfile
  114  bundle -v
  115  npx antora antora-playbook.yml
  116  grep gem ~/.??*
  117  vi ~/.alias
  118  npx antora antora-playbook.yml
  119  cat Gemfile
  120  vi Gemfile
  121  bundle install
  122  npx antora antora-playbook.yml
  123  gem install bigdecimal
  124  npx antora antora-playbook.yml
  125  gem list bigdecimal
  126  ruby -rbigdecimal -e "puts BigDecimal('123.45')"
  127  vi Gemfile
  128  bundle install
  129  npx antora antora-playbook.yml
  130  exp
  131  history
  132  cd ..
  133  ls
  134  cd submodel-templates/
  135  ls
  136  ls published/
  137  history
  138  history >> /f/Git/dsev2/docs/IDTA/Resource.md 
