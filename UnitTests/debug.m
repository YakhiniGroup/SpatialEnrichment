figure; hold on; PlotTesselationFiles('c:\PhD\ShortCompile\UnitTests\bin\Debug\coords_.csv','c:\PhD\ShortCompile\UnitTests\bin\Debug\lines_.csv')
PaintCell('c:\PhD\ShortCompile\UnitTests\bin\Debug\debug.csv')

%%
figure;
for i=0:11
    subplot(3,4,i+1)
    pts=csvread(horzcat('c:\PhD\ShortCompile\UnitTests\bin\Debug\coords_',num2str(i),'.csv'));
    a=gscatter(pts(:,1),pts(:,2),pts(:,3),'br','oo',[7 7]); 
    set(a(1), 'MarkerFaceColor', 'b'); set(a(2), 'MarkerFaceColor', 'r');
end
