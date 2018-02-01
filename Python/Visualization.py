import matplotlib.pyplot as plt
from mpl_toolkits.mplot3d import axes3d, Axes3D
import numpy as np
import csv


# CODE IS HERE
def plot_surface(a, b, c, d):
    # a plane is a*x+b*y+c*z+d=0
    # [a,b,c] is the normal.
    # d = -point.dot(normal)
    # create x,y mesh
    normal = np.array([a, b, c])
    xx, yy = np.meshgrid(np.arange(0.0, 1.0, 0.1), np.arange(0.0, 1.0, 0.1))
    # calculate corresponding z
    z = (-normal[0] * xx - normal[1] * yy - d) * 1. / normal[2]
    plt3d = plt.gca(projection='3d')
    plt3d.plot_surface(xx, yy, z, alpha=0.4)


def plot_coordinate(P, col=None):
    plt3d = plt.gca(projection='3d')
    plt3d.scatter(P[0], P[1], P[2], s=50, c=col)


def plot_coordinate(x, y, z):
    plt3d = plt.gca(projection='3d')
    plt3d.scatter(x, y, z, s=50)


def plot_problem_data_from_file(filename):
    plt.figure()
    with open(filename, 'r') as csvfile:
        data = csv.reader(csvfile, delimiter=',')
        coords = []
        cols = []
        for line in data:
            cols.append('b' if line[3] == '0' else 'r')
            coords.append([float(v) for v in line[0:3]])
        carr = np.asarray(coords)
        plt3d = plt.gca(projection='3d')
        plt3d.scatter(carr[:, 0], carr[:, 1], carr[:, 2], s=30, c=np.asarray(cols))


def plot_pivot_from_file(filename):
    with open(filename) as f:
        content = f.readlines()
    pval, thresh = content[0].split(',')
    pos = np.array([float(v) for v in content[1].split(',')])
    plt3d = plt.gca(projection='3d')
    plt3d.scatter(pos[0], pos[1], pos[2], s=50, c='k')


problemfile = r'C:\PhD\ShortCompile\SpatialEnrichment\bin\Debug\phosphoribosyltransferase_3d.csv '
plot_problem_data_from_file(problemfile)
pivotfile = r'C:\PhD\ShortCompile\SpatialEnrichment\bin\Debug\Cells\phosphoribosyltransferase_3d_Cell_0_0.csv'
plot_pivot_from_file(pivotfile)

# Create the figure
point1 = np.array([0.66625918,0.25990604,0.42158888])
point2 = np.array([0.08844277,0.21350691,0.69131287])
a, b, c, d = -0.90374796,-0.07257170,0.42186843,0.123459310520263

point1 = np.array([2.16999944,0.87723986,1.20092625])
point2 = np.array([2.20795155,1.88721871,0.63301012])
a, b, c, d =


fig = plt.figure()
plot_surface(a, b, c, d)
plot_coordinate(point1)
plot_coordinate(point2)
plt.gca().set_xlim3d(0, 1)
plt.gca().set_ylim3d(0, 1)
plt.gca().set_zlim3d(0, 1)


ax = Axes3D(fig)

X, Y, Z = axes3d.get_test_data(0.05)
ax.plot_surface(X, Y, Z, alpha=0.4)

cset = ax.contour(X, Y, Z, 16, extend3d=True)
ax.clabel(cset, fontsize=9, inline=1)

# and plot the point
ax.scatter(10, 10, 10, s=50, color='green')
plt.show()

point = np.array([1, 2, 3])
point2 = np.array([10, 50, 50])
# plot the surface
plt3d = plt.figure().gca(projection='3d')
# and i would like to plot this point :
