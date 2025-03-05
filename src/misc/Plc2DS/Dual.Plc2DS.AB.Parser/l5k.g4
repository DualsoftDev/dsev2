grammar l5k;

// Lexer rules


// Reserved keywords
CONTINUOUS: 'CONTINUOUS';
YES: 'Yes';
NO: 'No';


fragment DIGIT: [0-9];
ID: [A-Za-z_][A-Za-z0-9_]*;
STRING: '"' ('\\"' | ~["])* '"';  // 이스케이프 문자 처리 추가

INT: DIGIT+;
HEX: '16#' [0-9A-Fa-f_]+;
VERSION: DIGIT+ ('.' DIGIT+)*;
DOT: '.';
LBRACKET: '[';
RBRACKET: ']';
COLON: ':';
SEMI: ';';
WS: [ \t\r\n]+ -> skip;

// Comment support
LINE_COMMENT: '//' ~[\r\n]* -> skip;
BLOCK_COMMENT: '(*' .*? '*)' -> skip;


//ADDRESS: (~(' ' | '\t' | '\r' | '\n' | '(' | '"' | ';' | ',' ))+;  // 공백, '(' 및 ';' 이전까지 캡처
ADDRESS: ID+ (':' [A-Za-z0-9_]+)* ('.' [A-Za-z0-9_]+)* ('[' INT ']')?;


// Parser rules
l5kFile: ieVer 'CONTROLLER' controller 'END_CONTROLLER';

ieVer: 'IE_VER' ':=' VERSION ';';

controller:  ID '(' controllerParams ')' module* tag? program* task* config*;

controllerParams: paramList;

module: 'MODULE' ID '(' moduleParams ')' connection* 'END_MODULE';
moduleParams: paramList;

connection: 'CONNECTION' ID '(' connectionParams ')' SEMI 'END_CONNECTION';
connectionParams: paramList;

tag: 'TAG' tagEntry+ 'END_TAG';

tagEntry:
    ID 'OF' address
    '(' tagParams ')' SEMI;

address: ADDRESS ;

tagParams: paramList;

program: 'PROGRAM' ID '(' programParams ')' routine* childPrograms? 'END_PROGRAM';
programParams: paramList;

routine: 'ROUTINE' ID command* 'END_ROUTINE';

command: ID (':' commandArgs)? SEMI;
commandArgs: ID+;

task: 'TASK' ID '(' taskParams ')' ID SEMI 'END_TASK';
taskParams: paramList;

config: 'CONFIG' ID '(' configParams ')' configParam* 'END_CONFIG';
configParams: paramList;
configParam: param;

childPrograms: 'CHILD_PROGRAMS' program* 'END_CHILD_PROGRAMS';

// 공통 파라미터 리스트
paramList: param (',' param)*;
param: ID ':=' (STRING | INT | HEX | YES | NO | CONTINUOUS | ID);
