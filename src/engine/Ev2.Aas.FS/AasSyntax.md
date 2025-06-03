assetAdministrationShells
    assetAdministrationShell
        submodels
            reference
submodels
	submodel
		category..value
		submodelElements
			property
				category..value
			property
				category..value
            file
			submodelElementCollection
				category..(value)
				value
                    property
        				category..value
                    property
        				category..value
					submodelElementCollection
        				category..(value)
						value
                            property
        				        category..value
            file
			property
	submodel


- Json/Xml 특이사항
          "semanticId": {
            "keys": [
              {
                "type": "GlobalReference",
                "value": "urn:something00:f4547d0c"
              }
            ],
            "type": "ExternalReference"
          },


semanticId.Type =
	| ExternalReference
	| GlobalReference

keys.key.type =
	| GlobalReference
	| ConceptDescription		https://admin-shell.io/zvei/nameplate/1/0/ContactInformations/AddressInformation

qualifier.Type = 
	| SMT/Cardinality


value 는 단일 type 인 경우(string, integer, ..)와 복합 type 인 경우 다르게 처리해야 함
    - 단일 key
                  <valueType>xs:string</valueType>
                  <value></value>
    - 복합 type
          <value>
            <property>
            </property>
          </value>







AAS data type namespace : https://github.com/admin-shell-io/aas-specs-api/blob/main/Part1-MetaModel-Schemas/openapi.yaml#L282
    DataTypeDefXsd:
      type: string
      enum:
        - 'xs:anyURI'
        - 'xs:base64Binary'
        - 'xs:boolean'
        - 'xs:byte'
        - 'xs:date'
        - 'xs:dateTime'
        - 'xs:decimal'
        - 'xs:double'
        - 'xs:duration'
        - 'xs:float'
        - 'xs:gDay'
        - 'xs:gMonth'
        - 'xs:gMonthDay'
        - 'xs:gYear'
        - 'xs:gYearMonth'
        - 'xs:hexBinary'
        - 'xs:int'
        - 'xs:integer'
        - 'xs:long'
        - 'xs:negativeInteger'
        - 'xs:nonNegativeInteger'
        - 'xs:nonPositiveInteger'
        - 'xs:positiveInteger'
        - 'xs:short'
        - 'xs:string'
        - 'xs:time'
        - 'xs:unsignedByte'
        - 'xs:unsignedInt'
        - 'xs:unsignedLong'
        - 'xs:unsignedShort'