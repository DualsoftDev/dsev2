// FQDN (Fully Qualified Domain Name) Parser
// DOT(.) 으로 구분된 이름을 parsing.

grammar fqdn;
WS: [ \t\r\n]+ -> skip;
fqdns: fqdn (';' fqdn)* ';'? EOF;
fqdn: nameComponent ('.' | nameComponent)*;

Identifier: ValidIdStart ValidIdChar*;
   // lexical rule for hangul characters
    fragment HangulChar: [\u3131-\u314E\u314F-\u3163|\uAC00-\uD7A3]+;

    fragment ValidIdStart
    : ('a' .. 'z') | ('A' .. 'Z') | '_' | HangulChar
    ;

    fragment ValidIdChar
    : ValidIdStart | ('0' .. '9') | HangulChar
    ;

QuotedStringLiteral : '"' (~('"' | '\\' | '\r' | '\n') | '\\' ('"' | '\\'))* '"';

id: Identifier;
qid: QuotedStringLiteral;

// name component
nameComponent: id | qid;
