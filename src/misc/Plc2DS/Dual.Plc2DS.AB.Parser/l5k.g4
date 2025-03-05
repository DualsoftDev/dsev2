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

ADDRESS: ID+ (':' [A-Za-z0-9_]+)* ('.' [A-Za-z0-9_]+)* ('[' INT ']')?;


// Parser rules
l5kFile: ieVer 'CONTROLLER' controller 'END_CONTROLLER';

ieVer: 'IE_VER' ':=' VERSION ';';
params_block: '(' params ')';

controller:  ID params_block struct_def* module* tag? program* task* config*;

struct_def: 'DATATYPE' ID params_block (type_delc)+ 'END_DATATYPE';
    type_delc: typename varname params_block? SEMI;
        typename:
              'BOOL' | 'SINT' | 'INT' | 'DINT' | 'REAL'
            | 'LINT' | 'STRING' | 'TIME' | 'TOD' | 'DT' | 'DATE'
            | 'DTT' | 'ANY' | 'UDT' | 'ARRAY' | 'STRUCT';
        varname: ID (LBRACKET INT RBRACKET)?;
module: 'MODULE' ID params_block connection* 'END_MODULE';
connection: 'CONNECTION' ID params_block SEMI 'END_CONNECTION';

tag: 'TAG' tagEntry+ 'END_TAG';

tagEntry:
    ID 'OF' address
    params_block SEMI;

address: ADDRESS ;

program: 'PROGRAM' ID params_block routine* childPrograms? 'END_PROGRAM';
routine: 'ROUTINE' ID command* 'END_ROUTINE';

command: ID (':' commandArgs)? SEMI;
commandArgs: ID+;

task: 'TASK' ID params_block ID SEMI 'END_TASK';

config: 'CONFIG' ID params_block param* 'END_CONFIG';

childPrograms: 'CHILD_PROGRAMS' program* 'END_CHILD_PROGRAMS';

// 공통 파라미터 리스트
params: param (',' param)*;
param: ID ':=' (STRING | INT | HEX | YES | NO | CONTINUOUS | ID);
