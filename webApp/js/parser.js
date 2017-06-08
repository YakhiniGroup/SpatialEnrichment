

var parser = {
	csvFileToTextToJSON : function(csvFile, callbackFunctionToHandleResult, dataSetName, countryObject, isNew){
		var returnText = null;
		if(typeof(FileReader) != "undefined"){
			var reader = new FileReader();
			reader.onload = function (e) {
				returnText = reader.result;
				try{
					parser.csvTextToJSON(returnText, callbackFunctionToHandleResult, dataSetName, countryObject, isNew);
				}
				catch(err){
					alert(err);
				}
			}
			reader.readAsText(csvFile);
		}
		else{
			throw "Error : unable to create FileReader. This browser does not support HTML5";
		}
	},
	csvTextToJSON : function(csv, callback, dataSetName, countryObject, isNew){
		var newCsv = csv.replace(/\r/g, "\n")
		var lines = newCsv.split("\n");
		var result = [];
		var headers = lines[0].split(",");

		if(headers.length != 5 || headers[0] != "name" || headers[1] != "info" || headers[2] != "lon" || headers[3] != "lat" || headers[4] != "show"){
			throw "Error : CSV format isn't legal. Please check the 'Learn How' section for further information.";
		}
		for(var i = 1; i < lines.length; i++){
			var obj = {};
			var currentline =lines[i].split(",");
			for(var j = 0;j < headers.length; j++){
				obj[headers[j]] = currentline[j];
			}
			result.push(obj);
		}
		if(callback && dataSetName && countryObject){
			callback(result, dataSetName, countryObject, isNew);
		}  
	}
}

