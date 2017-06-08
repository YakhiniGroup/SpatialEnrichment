

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
		maps.initEvents();
	},
	initCountries : function(){
		maps.countries = new Array();
		/**
		var spots1 = [(new spot("spot1-1", 35,32,1,true)),(new spot("spot1-2",34.9,31.5,0,true)),(new spot("spot1-3",34.9,29.5,1,true)),(new spot("spot1-4",35.4,32.5,0,true))];
		var spots2 = [(new spot("spot2-1", 35.2,32.4,1,true)),(new spot("spot2-2",34.7,31.3,0,true)),(new spot("spot2-3",34.9,29.2,1,true)),(new spot("spot2-4",35.2,32.6,0,true))];
		var spots3 = [(new spot("spot3-1",-120,38,1,true)),(new spot("spot3-2",-95,36,0,true)),(new spot("spot3-3",-90,45,1,true)),(new spot("spot3-4",-73,43,0,true))];
		var spots4 = [(new spot("spot4-1",-121,38.5,1,true)),(new spot("spot4-2",-96,36.2,0,true)),(new spot("spot4-3",-90.3,45.4,1,true)),(new spot("spot4-4",-73.3,43.2,0,true))];
		var spotSetIsrael_1 = new spotSet("Demo Set Israel - 1", spots1, true);
		var spotSetIsrael_2 = new spotSet("Demo Set Israel - 2", spots2, true);
		**/
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
					maps.createMarker(spots[k].name, spots[k].info, spots[k].lon, spots[k].lat);
				};
			}
		};
	},
	addSpotSetSpots : function(spotSet){
		var spots = spotSet.spots;
		for (var i = 0; i < spots.length; i++) {
			maps.createMarker(spots[i].name, spots[i].info, spots[i].lon, spots[i].lat);
		};
	},
	createMarker : function(name, info, lon, lat){
		var el = document.createElement('div');
		el.className = 'marker';
		el.setAttribute("id", name);
		el.style.backgroundColor = (info == 0) ? "blue" : "red";
		el.style.height = "10px";
		el.style.width = "10px";
		el.style.borderRadius = "50%";
		el.style.visibility = "hidden";
		el.style.border = "white solid 1px";
		el.addEventListener("mouseover", function(){
		  	var markerPosition = $(this).position();
			var spotID = $(this).attr('id');
			panel.createAndDisplaySpotTag(spotID, markerPosition.top, markerPosition.left);
		});
		el.addEventListener("mouseleave", function(){
		  	panel.removeSpotTag();
		});
		new mapboxgl.Marker(el).setLngLat([lon, lat]).addTo(maps.map);
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
		var list = document.getElementsByClassName("marker");
		for(var i = 0; i < list.length; i++){
			list[i].style.visibility = "hidden";
		}
	},
	initEvents : function(){
	}
};
