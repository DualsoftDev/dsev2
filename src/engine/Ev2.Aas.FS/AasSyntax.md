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



value 는 단일 type 인 경우(string, integer, ..)와 복합 type 인 경우 다르게 처리해야 함
    - 단일 key
                  <valueType>xs:string</valueType>
                  <value></value>
    - 복합 type
          <value>
            <property>
            </property>
          </value>

