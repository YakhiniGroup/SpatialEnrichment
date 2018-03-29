addpath('c:\Users\shaybe\Dropbox\Thesis-PHd\SpatialEnrichment\')
filename='c:\Users\shaybe\Dropbox\Thesis-PHd\SpatialEnrichment\Repository\SpatialEnrichment\SpatialExperiments\bin\Debug\Experiments\aggregated_results.csv';
M=csvread(filename);

%% Compare opt pvalues on simulated data
%scatter (p_gird, p_sample) color / size is problem size. dont need opt for this, do N=100, 500, 1000.
p_sample=-log10(M(:,4))+log10(M(:, 8:4:end)); %compare to sample
%p_sample=repmat(-log10(M(:, 4)),1,6); %compare to opt
p_grid=-log10(M(:,4))+log10(M(:, 10:4:end));
numsamp=[100,1000,10000,20000,50000,100000];
%p_size=10+sqrt(repmat(numsamp,length(p_sample),1)); %num samples evaluated
p_size = .08*sqrt(repmat(M(:,3),1,6)); %num cells in problem
p_col = repmat(1:6,length(p_sample),1);
figure;
subplot(1,2,1)
%plot([0 22]',[0 22]','--k','linewidth',1); hold on;
colormap(lines(7))
scatter(p_grid(:),p_sample(:),p_size(:),p_col(:),'filled','markerfacealpha',.5)
title('size=#cells'); xlabel('Grid -Log(p-value)'); ylabel('Sampling -Log(p-value)');
cb = colorbar('Ticks',[1:6],'TickLabels',numsamp); cb.Label.String = '#evaluated'
%axis([0 22 0 9])
subplot(1,2,2)
scatter(p_grid(:)-p_sample(:),log10((p_size(:)/0.08).^2),50,p_col(:),'filled','markerfacealpha',.5)
xlabel('Grid-Sampling (-Log(p-value))'); ylabel('#Cells (Log_1_0)');
suptitle({'Opt enrichment comparison'}); 

%%
figure; 
col=lines(9);
idx=1;
for i=4:2:18
    idx=idx+1;
    xvals=M(:,3);%.*(1+rand(size(M(:,i)))*10);
    yvals=M(:,i);%.*(1+rand(size(M(:,i)))*10);
    scatter(xvals,yvals,50,col(idx,:),'filled','markerfacealpha',.7,'markeredgecolor','k');%,'jitter','on','jitteramount',100); 
    hold on;
end
set(gca,'xscale','log','yscale','log')
legend('opt','bead','cellsample@1k','uniform@1k','cellsample@10k','uniform@10k','cellsample@100k','uniform@100k');
xlabel('#Cells'); ylabel('#p-Value'); title('Single planted enrichment comparison.')

%%
figure; 
col=lines(9);
labels={'bead','cellsample@1k','uniform@1k','cellsample@10k','uniform@10k','cellsample@100k','uniform@100k'};
idx=1;
for i=6:2:18
    subplot(2,3,idx)
    xvals=M(:,3);%.*(1+rand(size(M(:,i)))*10);
    yvals=M(:,i);%.*(1+rand(size(M(:,i)))*10);
    scatter(M(:,3),M(:,4)-M(:,i),50,col(4,:),'filled','markerfacealpha',.7,'markeredgecolor','k');
    legend(horzcat(labels{idx},'-opt'));
    set(gca,'xscale','log','yscale','log')
    idx=idx+1;
end

%legend('opt','bead','cellsample@1k','uniform@1k','cellsample@10k','uniform@10k','cellsample@100k','uniform@100k');
xlabel('#Cells'); ylabel('#p-Value'); title('Single planted enrichment comparison.')

%% Plot instance
prfx='0';
fid=fopen(horzcat(cd,'\bin\Debug\Experiments\4\data_',prfx,'.csv'));
D=textscan(fid,'%f,%f,%f,%s',10);
fclose(fid);
D = [D{1} D{2} D{3} strncmp(D{4}, 'Tr', 2)];
figure;
scatter3(D(:,1),D(:,2),D(:,3),90,double(D(:,4)),'filled');
hold on;
opt=csvread(horzcat(cd,'\bin\Debug\Experiments\4\data_',prfx,'_optres.csv'));
scatter3(opt(:,1),opt(:,2),opt(:,3),30,'xk');
bead=csvread(horzcat(cd,'\bin\Debug\Experiments\4\data_',prfx,'_beadres.csv'));
scatter3(bead(:,1),bead(:,2),bead(:,3),60,'xc');    
planes=csvread(horzcat(cd,'\bin\Debug\Experiments\4\Planes.csv'));
syms x y
for i=1:3
    z=(-planes(i,1)*x -planes(i,2)*y - planes(i,4))/planes(i,3);
    fs=fsurf(z, 'MeshDensity',100, 'edgecolor','none','facecolor','k');
    fs.FaceAlpha=0.1;
    %alpha(0.9)
    %y=planes(i,4)/planes(i,2);
    %PaintPlane(planes(i,:),[0; y; 0]);
end
xlabel('x'); ylabel('y'); zlabel('z');
%axis([-1 2 -1 2 -1 2])
legend('data','opt', 'bead')


 
%% p_grid vs p_sample bubble
files=dir(horzcat(cd,'\bin\Debug\plane*.csv'));
for file={files.name}
    [h1,h2]=PaintPlaneFile(horzcat(horzcat(cd,'\bin\Debug\',file{:})))
    pause(5)
    delete(h1)
    delete(h2)
end

%% Investigate 2d difference between sampling and grid
cd('c:\phd\shortcompile\spatialexperiments')
fid=fopen(horzcat(cd,'\bin\Debug\Eval\dataset.csv'));
D=textscan(fid,'%f,%f,%s',20);
fclose(fid);
D = [D{1} D{2} strncmp(D{3}, 'Tr', 2)];
figure;
hold on;
blines = csvread(horzcat(cd,'\bin\Debug\Eval\bisectors.csv'));
linhndls=zeros(1,length(blines));
%for l=1:length(blines)
%    linhndls(l)=fplot(@(x) blines(l,1)*x+blines(l,2),'--k','linewidth',0.1);
%end
grid=csvread(horzcat(cd,'\bin\Debug\Eval\grid.csv'));
sampling=csvread(horzcat(cd,'\bin\Debug\Eval\sampling.csv'));
loci=csvread(horzcat(cd,'\bin\Debug\Eval\best.csv'));
%grd=scatter(grid(:,1),grid(:,2),0.1,'xk');
smpl=scatter(sampling(:,1),sampling(:,2),'.r');
bestloci=gscatter(loci(:,1),loci(:,2),[1 2],'gc','**');
scatter(D(:,1),D(:,2),60,double(D(:,3)),'filled'); 
axis([-1 2 -1 2])

%% Time VS Quality aggregate
resfolder='C:\Users\shaybe\Dropbox\Thesis-PHd\SpatialEnrichment\bSubtilis\Prepped\';
files = dir(horzcat(resfolder,'TimeQuality*'));
res = cell(length({files.name}));
restimes = cell(length({files.name}));
i=1;
for f = {files.name}
    display(f)
    gridData = csvread(horzcat(resfolder, f));
    samplingData = csvread(horzcat(resfolder, f));
    restimes(gridData)
    res{i}=log10(gridData(:,3))-log10(samplingData(:,3));
    i=i+1;
end
%


%% Time VS Quality
resfolder='C:\Users\shaybe\Dropbox\Thesis-PHd\SpatialEnrichment\bSubtilis\Prepped\';
%resfolder='C:\Users\shaybe\Dropbox\Thesis-PHd\SpatialEnrichment\Caulobacter\transferases\';
cd(resfolder)
files = dir(horzcat(resfolder,'TimeQuality_grid*'));
for f = {files.name}
    infile=f{:};
    gridData = csvread(horzcat(resfolder,infile));
    samplingData = csvread(horzcat(resfolder,strrep(infile,'TimeQuality_grid','TimeQuality_sampling')));
    fig=figure;
    
    fid=fopen(horzcat(resfolder,strrep(infile,'TimeQuality_grid_','')));
	D=textscan(fid,'%f,%f,%f,%f',527);
    fclose(fid);
    D = [D{1:4}];
    N=sum(D(:,4))*sum(not(D(:,4)));
    suptitle({strrep(strrep(infile,'_','-'),'TimeQuality-grid-','');horzcat('#Cells(log_1_0)=',num2str(log10(nchoosek(N,3)+nchoosek(N,2)+nchoosek(N,1)+1)))});
    subplot(1,2,1);
    scatter(gridData(:,1),gridData(:,2),'.r'); hold on;
    scatter(samplingData(:,1),samplingData(:,2),'.b')
    legend('Grid','Sampling','location','best');
    xlabel('Time(s)')
    ylabel('#Pivots(log_1_0)')
    subplot(1,2,2);
    scatter(gridData(:,1),-log10(gridData(:,3)),'.r'); hold on;
    scatter(samplingData(:,1),-log10(samplingData(:,3)),'.b')
    legend('Grid','Sampling','location','best');
    xlabel('Time(s)')
    ylabel('-log_1_0(p-Value)')
    saveas(fig, strrep(infile,'.csv','.png'));
%     subplot(2,2,[3 4]);
%     fid=fopen(horzcat('C:\Users\shaybe\Dropbox\Thesis-PHd\SpatialEnrichment\bSubtilis\Prepped\',strrep(infile,'TimeQuality_grid_','')));
%     D=textscan(fid,'%f,%f,%f,%f',527);
%     fclose(fid);
%     D = [D{1:4}];
%     scatter3(D(:,1),D(:,2),D(:,3),10,double(D(:,4)),'filled'); hold on;
%     scatter3(gridData(:,end-2),gridData(:,end-1),gridData(:,end),'xr');
%     df = diff(gridData(:,end-2:end));
%     %quiver3(gridData(1:end-1,end-2),gridData(1:end-1,end-1),gridData(1:end-1,end), df(:,1), df(:,2), df(:,3),'color','r')
%     scatter3(samplingData(:,end-2),samplingData(:,end-1),samplingData(:,end),'xg')
%     df = diff(samplingData(:,end-2:end));
%     %quiver3(samplingData(1:end-1,end-2),samplingData(1:end-1,end-1),samplingData(1:end-1,end), df(:,1), df(:,2), df(:,3),'color','r')
%     %line(samplingData(:,end-2),samplingData(:,end-1),samplingData(:,end),'color','b')
%     legend('Input','Grid','Sampling','location','best');
%     N=sum(D(:,4))*sum(not(D(:,4)));
%     title(horzcat('#Cells(log_1_0)=',num2str(log10(nchoosek(N,3)+nchoosek(N,2)+nchoosek(N,1)+1))))
%     stds=5*std(D(:,1:3));
%     axis([-stds(1) stds(1) -stds(2) stds(2) -stds(3) stds(3) ])
%     axis vis3d square
%     saveas(fig, strrep(infile,'.csv','.png'));
end

    