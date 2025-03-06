grammar l5k;

// Lexer rules

// Reserved keywords
CONTINUOUS: 'CONTINUOUS';
YES: 'Yes';
NO: 'No';
LBRACKET: '[';
RBRACKET: ']';
COLON: ':';
SEMI: ';';
COMMA: ',';
EXTENDEDPROP: 'ExtendedProp';
EXTENDEDPROP_VALUE: '[[[___' .*? '___]]]';        // EXTENDED_PROP

ROUTINE: 'ROUTINE' .*? 'END_ROUTINE'; 

fragment DIGIT: [0-9];
ID: [A-Za-z_][A-Za-z0-9_]*;
STRING: '"' ('\\"' | ~["])* '"';  // 이스케이프 문자 처리 추가
HASHNUM: DIGIT+ '#' [0-9A-Fa-f_]+;
INT: '-'? DIGIT+;
HEX: '16#' [0-9A-Fa-f_]+;
VERSION: DIGIT+ ('.' DIGIT+)*;
DOT: '.';
WS: [ \t\r\n]+ -> skip;

// Comment support
LINE_COMMENT: '//' ~[\r\n]* -> skip;
BLOCK_COMMENT: '(*' .*? '*)' -> skip;

// ADDRESS or VARNAME
VARNAME: ID+ (':' [A-Za-z0-9_]+)* ('.' [A-Za-z0-9_]+)* (LBRACKET INT RBRACKET)?;

// Parser rules
l5kFile: ieVer 'CONTROLLER' controller 'END_CONTROLLER';

ieVer: 'IE_VER' ':=' VERSION ';';
params_block: '(' params ')';

controller:  ID params_block struct_def* module* tag? program* task* config*;

struct_def: 'DATATYPE' ID params_block (type_decl)+ 'END_DATATYPE';
    type_decl: typename VARNAME params_block? SEMI;
        typename:
              'BOOL' | 'SINT' | 'INT' | 'DINT' | 'REAL'
            | 'LINT' | 'STRING' | 'TIME' | 'TOD' | 'DT' | 'DATE'
            | 'DTT' | 'ANY' | 'UDT' | 'ARRAY' | 'STRUCT'
            | 'SKT_Address' | 'SKT_SNTP_Data' | 'SKT_SNTP_Data'
            | 'SKT_Open_Connection' | 'SKT_Read_Source' | 'SKT_Read_Destination' | 'SKT_Write_Source';

module: 'MODULE' ID params_block body? connection*  'END_MODULE';

connection: 'CONNECTION' ID params_block body? 'END_CONNECTION';

tag: 'TAG' tagEntry* 'END_TAG';

tagEntry:
    ID 'OF' address
    params_block SEMI;

address: VARNAME ;

program: 'PROGRAM' ID params_block tag* ROUTINE* childPrograms? 'END_PROGRAM';
// routine: 'ROUTINE' ID command* 'END_ROUTINE';
routine: ROUTINE;   // routine 은 통짜로 무시.. 아래 command 관련 나중에 정리

command: ID (':' commandArgs)? SEMI;
commandArgs: ID+;

task: 'TASK' ID params_block ID SEMI 'END_TASK';

config: 'CONFIG' ID params_block decl* 'END_CONFIG';

childPrograms: 'CHILD_PROGRAMS' program* 'END_CHILD_PROGRAMS';

// 공통 파라미터 리스트
params: decl (',' decl)*;
decls: (decl SEMI)+;
body: (extenedProp)? decls+ (extenedProp)?;
decl: ID ':=' (value | list);
extenedProp: EXTENDEDPROP ':=' EXTENDEDPROP_VALUE;      // no trailing SEMI

// 값 정의 (기존 타입 유지)
value: STRING | INT | HEX | YES | NO | HASHNUM | CONTINUOUS | ID | EXTENDEDPROP_VALUE;

// 리스트 정의 (중첩 리스트 지원)
list: '[' elements? ']';
elements: (value | list) (',' (value | list))*;
