

var JSONobject = {
	spotSet : null,
	spots : null,
	initObject : function(spotSet){
		JSONobject.spotSet = spotSet;
		JSONobject.initSpots(JSONobject.spotSet.spots);
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
	reInitObject : function(){
		JSONobject.initObject(JSONobject.spotSet);
	},
	sendJsonObjectToServer : function(){
		console.log("sending JSONobject to server : \n" + JSON.stringify(JSONobject.spots));
		panel.activateLoader();
		$.ajax({
			url: 'https://xfg3e9bjw7.execute-api.us-west-2.amazonaws.com/prod/data',
			type: 'POST',
			contentType: 'application/json',
			success : function(json){
				panel.exitLoader();
				alert("Server response : " + JSON.stringify(json));
				console.log(JSON.stringify(json));
			},
			error : function(err){
				panel.exitLoader();
				alert("Error : Failure connecting to server");
			},
			data : JSON.stringify(JSONobject.spots)
		});
	}
}

