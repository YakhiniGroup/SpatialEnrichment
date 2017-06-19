$(document).ready(function(){
	app.init();
});

var app = {
	init : function(){
		maps.init();
		panel.init();
		uploadDataSetPanel.init();
		appEvents.init();
	}
}

function spot(name, lon, lat, info, show){
	this.name = name;
	this.lon = lon;
	this.lat = lat;
	this.info = info;
	this.show = show;
}

function SpatialmHGResult(lon, lat, mHGthreshold, pValue){
	this.lon = lon;
	this.lat = lat;
	this.mHGthreshold = mHGthreshold;
	this.pValue = pValue;
}

function spotSet(name, spots, show){
	this.name = name;
	this.spots = spots;
	this.show = show;
}

function country(countryName, lon, lat, zoom, setOfSpotSets){
	this.countryName = countryName;
	this.lon = lon;
	this.lat = lat;
	this.zoom = zoom;
	this.setOfSpotSets = setOfSpotSets;
}


var appEvents = {
	init : function(){
		$("#calculateButton").click(function(){
			if(JSONobject.spotSet == null){
				alert("Choose a country and set of spots first!")
			}
			else{
				JSONobject.sendJsonObjectToServer();
			}
		});
	}
}


window.onclick = function(event) {
	var spotList = document.getElementsByClassName("searchSpotList");
	var searchBar = document.getElementById("selectSpotSearchBar");
    if(event.target != spotList[0] && event.target != searchBar){
	    $(".searchSpotList").css("display" ,"none");
    }
}

function ajaxCall(URL, callback, arg1, arg2, arg3){
    $.ajax({
        url: URL,
        success: function(data){
           if(callback){
           	  callback(data, arg1, arg2, arg3);
           }
        },
        error: function(err){
        	console.log("Error: Failed to load data sets");
        }
    });
}



