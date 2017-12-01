

var JSONobject = {
	spotSet : null,
	spots : null,
	params: null,
	isProcessedByServer: false,
	initObject : function(spotSet){
		JSONobject.spotSet = spotSet;
		JSONobject.initSpots(JSONobject.spotSet.spots);
		JSONobject.initParams();
	},
	initSpots : function(spotsArr){
		spotsArray = [];
		for (var i = 0; i < spotsArr.length; i++) {
			if(spotsArr[i].show){
				spotsArray.push(spotsArr[i]);
			}		
		};
		JSONobject.spots = {
			spots : spotsArray
		}
	},
	initParams : function(){
		var Actions = $("#configParamsForm_Actions").val();
		var SkipSlack = -2;
		var Threshold = $("#configParamsForm_Threshold_Ranger").val();
		var ExecutionTokenId = Math.floor(Math.random() * 10000);
		JSONobject.params = {
			actions : Actions,
			skipSlack : SkipSlack,
			threshold : Threshold,
			executionTokenId : ExecutionTokenId
		}
	},
	reInitObject : function(){
		JSONobject.initObject(JSONobject.spotSet);
	},
	sendJsonObjectToServer : function(){
		JSONobject.isProcessedByServer = true;
		panel.activateLoader();
		var object = {
			spots : JSONobject.spots,
			parameters : JSONobject.params,
		}
		console.log(JSON.stringify(object));
		$.ajax({
			url: 'https://spatialenrichmentfunc.azurewebsites.net/api/HttpTriggerCSharp',
			type: 'POST',
			contentType: 'application/json',
			success : function(json){
				JSONobject.isProcessedByServer = false;
				console.log(JSON.stringify(json));
				var SpatialmHGResultArray = parser.JSONToSpatialmHGResultArray(json);
				maps.addSpatialmHGResultSpotsToMap(SpatialmHGResultArray);
			},
			error : function(xhr, status, error){
				JSONobject.isProcessedByServer = false;
			},
			data : JSON.stringify(object)
		});
		panel.updateLoader(JSONobject.params.executionTokenId, false);
	}
}

