이 문법은 Antora를 사용하는 문서 사이트(예: Red Hat Docs, Eclipse Documentation 등)에서 매우 흔히 쓰이는 형식입니다.
```
[#index:::_sequence_control]
== Submodel Template for "Sequence Control"

<<index:::_sequence_control>> 절 참고
```

index:::_sequence_control은 Antora에서 문서/컴포넌트 네임스페이스 체계를 따를 때 사용되는 fully qualified ID입니다.

    index → 문서 (예: index.adoc)

    _sequence_control → 해당 문서 내의 ID(anchor)

[#id] 또는 [[id]]	현재 문서 내 ID 지정	[#sequence-control]
<<id,label>>	현재 문서 내 ID 참조	<<sequence-control,Sequence Control>>
<<filename::id>>	다른 문서의 ID 참조 (Antora)	<<index::_sequence_control>>
xref:filename.adoc#id[Label]	명시적 파일 + ID 링크	xref:index.adoc#_sequence_control[Go to Section]



1. [discrete#ID]

이 표현은 다음 두 가지 기능을 결합한 것입니다:

    discrete: 제목을 목차(TOC)에는 포함하지 않고, 스타일만 section title처럼 보이게 만들라는 의미입니다. 즉, 논리적인 섹션은 아님.

    #index:::_imprint: 해당 블록의 ID (anchor) 입니다. Antora 스타일의 fully qualified ID입니다.