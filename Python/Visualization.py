import matplotlib.pyplot as plt
from mpl_toolkits.mplot3d import axes3d, Axes3D
import numpy as np

# Create the figure
fig = plt.figure()

ax = Axes3D(fig)

X, Y, Z = axes3d.get_test_data(0.05)
ax.plot_surface(X, Y, Z, alpha=0.4)

cset = ax.contour(X, Y, Z, 16, extend3d=True)
ax.clabel(cset, fontsize=9, inline=1)

# and plot the point
ax.scatter(10, 10, 10, s=50, color='green')
plt.show()

# CODE IS HERE
def plot_surface(a, b, c, d):
    # a plane is a*x+b*y+c*z+d=0
    # [a,b,c] is the normal. Thus, we have to calculate
    # d and we're set
    # d = -point.dot(normal)
    # create x,y
    normal = np.array([a, b, c])
    xx, yy = np.meshgrid(range(5), range(5))
    # calculate corresponding z
    z = (-normal[0] * xx - normal[1] * yy - d) * 1. /normal[2]
    plt3d = plt.gca(projection='3d')
    plt3d.plot_surface(xx, yy, z, alpha=0.4)

def plot_coordinate(x,y,z):
    plt3d = plt.gca(projection='3d')
    plt3d.scatter(x, y, z, s=50)

point  = np.array([1, 2, 3])
point2 = np.array([10, 50, 50])
# plot the surface
plt3d = plt.figure().gca(projection='3d')
#and i would like to plot this point :

