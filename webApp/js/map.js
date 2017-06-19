

var maps = {
	countries : null,
	map : null,
	init : function(){
		maps.initCountries();
		//maps.initPreloadedData();
		mapboxgl.accessToken = 'pk.eyJ1IjoiYXNhZnJpZWQiLCJhIjoiY2owa3QwZ3R3MDFlMDMzcDI4N2ptcGFuayJ9.5VD0vjYPdxm16RdDcKPT5A';
		maps.map = new mapboxgl.Map({
		    container: 'map', // container id
		    style: 'mapbox://styles/mapbox/streets-v10', //stylesheet location
		    center: [34.999,32.025], // starting position
		    zoom: 0 // starting zoom
		});
		maps.addAllSpots();
		maps.addPreloadedSets();
	},
	initCountries : function(){
		maps.countries = new Array();
		maps.countries.push(new country("World", 0, 0, 0, []));
		maps.countries.push(new country("Israel", 35, 31.1, 6.5, []));
		maps.countries.push(new country("United States", -95, 36, 3.5,[]));
		maps.countries.push(new country("Africa", 34.5085, 8.7832, 2.4,[]))
	},
	addPreloadedSets : function(){
		var unitedStates = maps.returnCountryObject("United States");
		var africa = maps.returnCountryObject("Africa");
		ajaxCall("data/earthquakes.csv", parser.csvTextToJSON, panel.addDateSetToPanel, "Earthquakes 1956 - 2016 (Mag 4.5 +)", unitedStates);
		ajaxCall("data/employment.csv", parser.csvTextToJSON, panel.addDateSetToPanel, "2016 - 2017 Employment rate increase y/n", unitedStates);
		ajaxCall("data/elections.csv", parser.csvTextToJSON, panel.addDateSetToPanel, "USA 2016 Elections", unitedStates);
		ajaxCall("data/terror.csv", parser.csvTextToJSON, panel.addDateSetToPanel, "US state capitals terror attacks y/n 2015 - 2015", unitedStates);
		ajaxCall("data/zika.csv", parser.csvTextToJSON, panel.addDateSetToPanel, "Active Zika Virus Transmission", africa);
		ajaxCall("data/earthquakes.csv", parser.csvTextToJSON, panel.addDateSetToPanel, "Earthquakes 1956 - 2016 (Mag 4.5 +)", unitedStates);
	},
	setCenter : function(lon, lat){
		maps.map.setCenter([lon, lat]);
	},
	setZoom : function(zoom){
		maps.map.setZoom(zoom);
	},
	flyTo : function(lon, lat, zoom){
		maps.map.flyTo({
	        // These options control the ending camera position: centered at
	        // the target, at zoom level 9, and north up.
	        center: [lon, lat],
	        zoom: zoom,
	        bearing: 0,
	        // These options control the flight curve, making it move
	        // slowly and zoom out almost completely before starting
	        // to pan.
	        speed: 2.5, 
	        curve: 1, 
	        // This can be any easing function: it takes a number between
	        // 0 and 1 and returns another number between 0 and 1.
	        easing: function (t) {
	            return t;
	        }
	    });
	},
	returnCountryObject : function(name){
		for(var i = 0; i < maps.countries.length; i++){
			if(maps.countries[i].countryName == name){
				return maps.countries[i];
			}
		};
	},
	returnSpotSetObject : function(name){
		for(var i = 0; i < maps.countries.length; i++){
			var setOfSpotSets = maps.countries[i].setOfSpotSets;
			for(var j = 0; j < setOfSpotSets.length; j++){
				if(setOfSpotSets[j].name == name){
					return setOfSpotSets[j];
				}	
			}	
		};
	},
	changeSpotShow : function(spotID, show){
		for(var i = 0; i < maps.countries.length; i++){
			var setOfSpotSets = maps.countries[i].setOfSpotSets;
			for(var j = 0; j < setOfSpotSets.length; j++){
				for(var k = 0; k < setOfSpotSets[j].spots.length; k++){
					if(setOfSpotSets[j].spots[k].name == spotID){
						setOfSpotSets[j].spots[k].show = show;
						return;
					}
				};
			}	
		};
	},
	returnSpotObject : function(spotID){
		for(var i = 0; i < maps.countries.length; i++){
			var setOfSpotSets = maps.countries[i].setOfSpotSets;
			for(var j = 0; j < setOfSpotSets.length; j++){
				for(var k = 0; k < setOfSpotSets[j].spots.length; k++){
					if(setOfSpotSets[j].spots[k].name == spotID){
						return setOfSpotSets[j].spots[k];
					}
				};
			}	
		};	
	},
	addAllSpots : function(){
		for (var i = 0; i < maps.countries.length; i++){
			var setOfSpotSets = maps.countries[i].setOfSpotSets;
			for(var j = 0; j < setOfSpotSets.length; j++){
				var spots = setOfSpotSets[j].spots;
				for (var k = 0; k < spots.length; k++) {
					maps.createMarker(spots[k]);
				};
			}
		};
	},
	addSpotSetSpots : function(spotSet){
		var spots = spotSet.spots;
		for (var i = 0; i < spots.length; i++) {
			maps.createMarker(spots[i]);
		};
	},
	addSpatialmHGResultSpotsToMap : function(arrayOfSpatialmHGResultSpots){
		for (var i = 0; i < arrayOfSpatialmHGResultSpots.length; i++) {
			maps.createSpatialmHGResultMarker(arrayOfSpatialmHGResultSpots[i]);
		};
	},
	createMarker : function(spot){
		var el = document.createElement('div');
		el.className = 'marker';
		el.setAttribute("id", spot.name);
		el.style.backgroundColor = (spot.info == 0) ? "blue" : "red";
		el.style.height = "10px";
		el.style.width = "10px";
		el.style.borderRadius = "50%";
		el.style.visibility = "hidden";
		el.style.border = "white solid 1px";
		el.addEventListener("mouseover", function(){
		  	var markerPosition = $(this).position();
			panel.createAndDisplaySpotTag(maps.returnSpotObject(spot.name), markerPosition.top, markerPosition.left);
		});
		el.addEventListener("mouseleave", function(){
		  	panel.removeSpotTag();
		});
		new mapboxgl.Marker(el).setLngLat([spot.lon, spot.lat]).addTo(maps.map);
	},
	createSpatialmHGResultMarker : function(SpatialmHGResultSpot){
		var size = (SpatialmHGResultSpot.pValue * 10 + 10) + "px";
		var el = document.createElement('div');
		el.className = 'spatialmHGResultMarker';
		el.style.backgroundColor = 'green';
		el.style.height = size;
		el.style.width = size;
		el.style.borderRadius = "50%";
		el.style.visibility = "visible";
		el.addEventListener("mouseover", function(){
		  	var markerPosition = $(this).position();
			panel.createAndDisplaySpotTag(SpatialmHGResultSpot, markerPosition.top, markerPosition.left);
		});
		el.addEventListener("mouseleave", function(){
		  	panel.removeSpotTag();
		});
		new mapboxgl.Marker(el).setLngLat([SpatialmHGResultSpot.lon, SpatialmHGResultSpot.lat]).addTo(maps.map);
	},
	showSpotsOnMapForGivenSpotSet : function(spotSet){
		maps.cleanAllSpotsFromMap();
		for (var i = 0; i < spotSet.spots.length; i++){
			if(spotSet.spots[i].show){
				$("#"+spotSet.spots[i].name).css("visibility","visible");
			}
		}
	},
	hideSpot : function(id){
		$("#"+id).css("visibility","hidden");
		maps.changeSpotShow(id, false);
	}, 
	showSpot : function(id){
		$("#"+id).css("visibility","visible");
		maps.changeSpotShow(id, true);
	},
	cleanAllSpotsFromMap : function(){
		var listOfMarkers = document.getElementsByClassName("marker");
		for(var i = 0; i < listOfMarkers.length; i++){
			listOfMarkers[i].style.visibility = "hidden";
		}
		$(".spatialmHGResultMarker").remove();
	}
};
