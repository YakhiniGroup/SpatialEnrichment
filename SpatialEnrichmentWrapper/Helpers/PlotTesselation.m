function PlotTesselation(ptsX, ptsY)
pts=[ptsX ptsY];
l1=1:length(pts); l2=1:length(pts);
figure; gscatter(pts(:,1),pts(:,2),label);
hold on;
for ti=1:length(l1)
    for tj=(ti+1):length(l2)
        i=l1(ti); j=l2(tj);
        midpt=(pts(i,:)+pts(j,:))./2;
        slope=pts(i,:)-pts(j,:); slope=-slope(1)/slope(2);
        b=midpt(2)-slope*midpt(1);
        %y=0 / y=1; solve for x
        line([b/(-slope) (1-b)/slope],[0 1],'Linestyle','--','color','r');
    end
end