

var panel = {
	spotSet : null,
	init : function(){
		panel.resetSpotSet();
		panel.initCountrySelection();
		panel.initSpotSetsSelction();
		panel.initSpots();
		panel.initEvents();
	},
	refreshPanel : function(){
		//TODO: remove all il's 
		$('#panel .selectCountryDropdown ul').empty();
		$('#panel .searchSpotList').empty();
		$('#panel .selectDataSetDropdown ul').empty();
		panel.init();
	},
	initCountrySelection : function(){
		maps.countries.forEach(function(country){
			$('#panel .selectCountryDropdown ul').append('<li><a href="#">' + country.countryName +'</li>');
		});
	},
	initSpots : function(){
		for (var i = 0; i < maps.countries.length; i++) {
			var setOfSpotSets = maps.countries[i].setOfSpotSets;
			for (var j = 0; j < setOfSpotSets.length; j++) {
				var spots = setOfSpotSets[j].spots;
				for (var k = 0; k < spots.length; k++) {
					$('#panel .searchSpotList').append('<li class="checkbox"><label><input type="checkbox" value=' +spots[k].name +' checked>' + spots[k].name +'</label></li>');
				}
			}
		}
		document.getElementsByClassName("searchSpotList")[0].style.display = "none";
		panel.addSelectColorBoxToCheckboxClasses();
		panel.cleanSpotsFromPanel();		
	},
	initSpotSetsSelction : function(){
		for (var i = 0; i < maps.countries.length; i++){
			var setOfSpotSets = maps.countries[i].setOfSpotSets;
			for (var j = 0; j < setOfSpotSets.length; j++){
				$('#panel .selectDataSetDropdown ul').append('<li class="spotSet"><a href="#">' + setOfSpotSets[j].name +'</li>');
			}
		}
		panel.cleanSpotSetsFromPanel();	
	},
	setSpotSet : function(spotSet){
		panel.spotSet = spotSet;
	},
	resetSpotSet : function(){
		panel.spotSet = {spots:[]};
	},
	displaySpotSetSpots : function(spotSet){
		var found;
		var spots = spotSet.spots;
		var list = document.getElementsByClassName("checkbox");
		for(var i = 0; i < list.length; i++){
			found = false;
			//this is a label element
			var listElement = list[i].childNodes[0];
			for(var j = 0; j < spots.length; j++){
				if(spots[j].name == listElement.textContent){
					list[i].style.display= "block";
					found = true;
					break;
				}
			}
			if(!found)
				list[i].style.display = "none";
		}
	},
	displayCountrySetOfSpotSets : function(country){
		var found;
		var setOfSpotSets = country.setOfSpotSets;
		var list = document.getElementsByClassName("spotSet");
		for(var i = 0; i < list.length; i++){
			found = false;
			//this is a label element
			var listElement = list[i].childNodes[0];
			for(var j = 0; j < setOfSpotSets.length; j++){
				if(setOfSpotSets[j].name == listElement.textContent){
					list[i].style.display="block";
					found = true;
					break;
				}
			}
			if(!found)
				list[i].style.display = "none";
		}
	},
	createAndDisplaySpotTag: function(spot, topPosition, leftPosition){
		var spotObject = spot;
		var spotTagElement = document.createElement("div");
		spotTagElement.className = 'spotTag';
		spotTagElement.style.top = topPosition+"px";
		spotTagElement.style.left = leftPosition+"px";
		document.getElementById("map").appendChild(spotTagElement);
		for (var property in spotObject) {
		    if (spotObject.hasOwnProperty(property)) {
		    	var spotTagPropertyElement = document.createElement("div");
		    	spotTagPropertyElement.className='spotTagProperty';
		    	spotTagPropertyElement.innerHTML = property + " : " + spotObject[property];
		    	document.getElementsByClassName("spotTag")[0].appendChild(spotTagPropertyElement);
		    }
		}
	},
	cleanSpotsFromPanel : function(){
		var list = document.getElementsByClassName("checkbox");
		for(var i = 0; i < list.length; i++){
			list[i].style.display = "none";
		}
	},
	cleanSpotSetsFromPanel : function(){
		var list = document.getElementsByClassName("spotSet");
		for(var i = 0; i < list.length; i++){
			list[i].style.display = "none";
		}
	},
	removeSpotTag : function(id){
		$('.spotTag').remove();
	},
	addSelectColorBoxToCheckboxClasses : function(){
		var list = document.getElementsByClassName("checkbox");
		for(var i = 0; i < list.length; i++){
			var colorBoxElement = document.createElement('div');
			var redColorBox = document.createElement('div');
			var blueColorBox = document.createElement('div');
			var idOFColorBoxRed = document.createElement('input');
			var idOFColorBoxBlue = document.createElement('input');
			colorBoxElement.className = 'colorBoxElement';
			redColorBox.className = 'colorBox';
			redColorBox.style.backgroundColor = "red";
			blueColorBox.className = 'colorBox';
			blueColorBox.style.backgroundColor = "blue";
			idOFColorBoxRed.setAttribute("type", "hidden");
			idOFColorBoxRed.setAttribute("value", list[i].childNodes[0].childNodes[0].getAttribute("value"));
			idOFColorBoxBlue.setAttribute("type", "hidden");
			idOFColorBoxBlue.setAttribute("value", list[i].childNodes[0].childNodes[0].getAttribute("value"));
			list[i].appendChild(colorBoxElement);
			colorBoxElement.appendChild(redColorBox);
			colorBoxElement.appendChild(blueColorBox);
			redColorBox.appendChild(idOFColorBoxRed);
			blueColorBox.appendChild(idOFColorBoxBlue);
		}
	},
	updateSelectSpotSearchBar : function(){
		var selectSpotSearchBar, filter, searchSpotList, li, nameOfSpot, i, j;
	    selectSpotSearchBar = document.getElementById('selectSpotSearchBar');
	    filter = selectSpotSearchBar.value.toUpperCase();
	    searchSpotList = document.getElementsByClassName("searchSpotList");
	    li = searchSpotList[0].getElementsByTagName('li');
	    for (i = 0; i < li.length; i++) {
	        nameOfSpot = li[i].getElementsByTagName("input")[0].getAttribute("value");
	        if (nameOfSpot.toUpperCase().indexOf(filter) > -1) {
	            for(j = 0; j < panel.spotSet.spots.length; j++){
	        		if(panel.spotSet.spots[j].name == nameOfSpot)
	        			li[i].style.display = "block";
	        	}
	        } else {
	            li[i].style.display = "none";
	        }
	    }
	},
	addDateSetToPanel : function(setOfSpots, nameOfDataSet, countryObject, isNew){
		var userSpotSet = new spotSet(nameOfDataSet, setOfSpots, true);
		countryObject.setOfSpotSets.push(userSpotSet);
		panel.refreshPanel();
		maps.addSpotSetSpots(userSpotSet);
		maps.cleanAllSpotsFromMap();
		if(isNew){
			alert("Success : Data set uploaded!");
		}
	},
	activateLoader : function(){
		panel.resetLoader();
		$("#progressModal").modal({
			  backdrop: 'static',
  			  keyboard: false
		});
	},
	resetLoader : function(){
		panel.assignValueToLoader(0);
		panel.assignMessageToLoader("Starting Process..")
	},
	exitLoader : function(){
		$("#progressModal").modal("hide");
	},
	updateLoader : function(queryId){
		// ajax call to azure function
		$.ajax({
			url:"https://spatialenrichmentdatabasequeryfunc.azurewebsites.net/api/HttpTriggerCSharp",
			type: 'POST',
			contentType: 'application/json',
			success: function(json){
				var value = json.Value;
				var message = json.Message;
				panel.assignValueToLoader(value);
				panel.assignMessageToLoader(message);
				if(JSONobject.isProcessedByServer){
					//wait 3 second, then update again
					setTimeout(function(){panel.updateLoader(queryId);}, 3000);
				}
				else{
					panel.exitLoader();
				}
			},
			error : function(xhr, status, error){
				alert("Error : Failure connecting to server\r\n" + "Status : " + xhr.status + "\r\n" + "Message: " + error);
				panel.exitLoader();
			},
			data: JSON.stringify({id : queryId})
		});
		//update progress bar
		// var value = $("#progressModal .progress-bar").attr("aria-valuenow");
		// var newValue = parseInt(value) + 5;
		// panel.assignValueToLoader(newValue);
		// if(JSONobject.isProcessedByServer){
		// 	//wait 3 second, then update again
		// 	setTimeout(function(){panel.updateLoader(queryId);}, 3000);
		// }
		// else{
		// 	// server finshed progress
		// 	panel.exitLoader();
		// }
	},
	assignValueToLoader : function(newValue){
		if(newValue <= 100 && newValue >=0){
			$("#progressModal .progress-bar").attr("aria-valuenow",  newValue);
			$("#progressModal .progress-bar").css("width" , newValue + "%");
			$("#progressModal .progress-bar").text(newValue + "%");
		}
	},
	assignMessageToLoader : function(message){
		$(".progress-message").text(message);
	},
	initEvents : function(){
		$("#panel .selectCountryDropdown li").click(function(){
			var country = maps.returnCountryObject($(this).text());
			maps.flyTo(country.lon, country.lat, country.zoom);
			panel.displayCountrySetOfSpotSets(country);
			panel.cleanSpotsFromPanel();
			panel.resetSpotSet();
			maps.cleanAllSpotsFromMap();
		});
		$(".colorBox").click(function(){
			var colorOfBox = $(this).css("background-color");
			var inputArray = $(this).children("input");
			var idOfBox = inputArray[0].getAttribute("value");
			var marker = document.getElementById(idOfBox);
			var spot = maps.returnSpotObject(idOfBox);
			marker.style.backgroundColor = colorOfBox;
			spot.info = (colorOfBox == "rgb(0, 0, 255)") ? 0 : 1; 
			JSONobject.reInitObject();
		});
		$("#panel .selectDataSetDropdown li").click(function(){
			var spotSet = maps.returnSpotSetObject($(this).text());
			panel.displaySpotSetSpots(spotSet);
			maps.showSpotsOnMapForGivenSpotSet(spotSet);
			panel.setSpotSet(spotSet);
			JSONobject.initObject(spotSet);
		});
		$(".checkbox input").click( function(){
		   var id = ($(this).parent()).text();
		   if( $(this).is(':checked')){
		   	maps.showSpot(id);
		   	JSONobject.reInitObject();
		   }
		   else{
		   	maps.hideSpot(id);
		   	JSONobject.reInitObject();
		   }
		});
		$("#selectSpotSearchBar").click(function(){
			$(".searchSpotList").css("display" ,"");
		});
		$("#selectSpotSearchBar").keyup(function(){
			panel.updateSelectSpotSearchBar();
			$(".searchSpotList").css("display" ,"");
		});
        $("#configParamsForm_Threshold_Ranger, #configParamsForm_Threshold_Number").change(function(event){
        	var val = $(this).val();
			var max = $(this).attr("max");
			var min = $(this).attr("min");
			if(val > max || val < min){
				event.preventDefault();
				return;
			}
        	$("#configParamsForm_Threshold_Ranger").val(val);
        	$("#configParamsForm_Threshold_Number").val(val);
        });
        $("#configParamsForm input, #configParamsForm select").change(function(){
			JSONobject.initParams();
        });
	}
}



var uploadDataSetPanel = {
	file : null,
	selectedCountry : null,
	nameOfDataSet : null,
	init : function(){
		uploadDataSetPanel.nameOfDataSet = "";
		this.loadCountries();
		this.initEvents();
	},
	initEvents : function(){
		$("#uploadCsvFileButton").click(function(){
			if (uploadDataSetPanel.selectedCountry == null || uploadDataSetPanel.nameOfDataSet == "") {
				alert("Select country and enter name of data set first!");
			}else{
	        	$("#hiddenUploadCsvFileButton").click();
	    	}
	    });
	    $("#DataSetUploadPanel .dropdown ul li").click(function(){
			uploadDataSetPanel.selectedCountry = maps.returnCountryObject($(this).text());
			$("#nameTagOfSelectedCountryInDataSetUploadPanel").text(uploadDataSetPanel.selectedCountry.countryName);
		});
		$("#DataSetUploadPanel input[type='file']").change(function(){
			if($(this).val() != "" && uploadDataSetPanel.csvFileApprove()){
	        	uploadDataSetPanel.file = document.getElementById('hiddenUploadCsvFileButton').files[0];
	        	try{
	        		parser.csvFileToTextToJSON(uploadDataSetPanel.file, panel.addDateSetToPanel, uploadDataSetPanel.nameOfDataSet, uploadDataSetPanel.selectedCountry, true);
	       		}
	       		catch(err){
	       			alert(err);
	       		}
	        	//parser.csvTextToJSON(returnText);
			}else{
				alert("Fail to upload file : make sure it is CSV");
			}
        });
        $("#inputNameOfUploadedDataSet").keyup(function(){
        	uploadDataSetPanel.nameOfDataSet = $(this).val();
        });
	},
	loadCountries : function(){
		//TODO : load list of countries from Map object
		maps.countries.forEach(function(country){
			if(country.countryName != "World"){
				$('#DataSetUploadPanel .dropdown ul').append('<li><a href="#">' + country.countryName +'</li>');
			}
		});
	},
	//TODO : check regex format for other browsers beside chrome
	csvFileApprove : function(){
		var type = document.getElementById('hiddenUploadCsvFileButton').files[0].type;
		var pattCsv = /^text\/csv/;

		return pattCsv.test(type)
	}
}




