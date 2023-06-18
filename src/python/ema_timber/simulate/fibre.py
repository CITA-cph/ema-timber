import math



def get_velocity(strength, xs, ys, X, Y):
    """
    From https://nbviewer.org/github/barbagroup/AeroPython/blob/master/lessons/03_Lesson03_doublet.ipynb
    
    Returns the velocity field generated by a source/sink.
    
    Parameters
    ----------
    strength: float
        Strength of the source/sink.
    xs: float
        x-coordinate of the source (or sink).
    ys: float
        y-coordinate of the source (or sink).
    X: 2D Numpy array of floats
        x-coordinate of the mesh points.
    Y: 2D Numpy array of floats
        y-coordinate of the mesh points.
    
    Returns
    -------
    u: 2D Numpy array of floats
        x-component of the velocity vector field.
    v: 2D Numpy array of floats
        y-component of the velocity vector field.
    """
    u = strength / (2 * numpy.pi) * (X - xs) / ((X - xs)**2 + (Y - ys)**2)
    v = strength / (2 * numpy.pi) * (Y - ys) / ((X - xs)**2 + (Y - ys)**2)
    
    return u, v



def get_stream_function(strength, xs, ys, X, Y):
    """
    From https://nbviewer.org/github/barbagroup/AeroPython/blob/master/lessons/03_Lesson03_doublet.ipynb

    Returns the stream-function generated by a source/sink.
    
    Parameters
    ----------
    strength: float
        Strength of the source/sink.
    xs: float
        x-coordinate of the source (or sink).
    ys: float
        y-coordinate of the source (or sink).
    X: 2D Numpy array of floats
        x-coordinate of the mesh points.
    Y: 2D Numpy array of floats
        y-coordinate of the mesh points.
    
    Returns
    -------
    psi: 2D Numpy array of floats
        The stream-function.
    """
    psi = strength / (2 * numpy.pi) * numpy.arctan2((Y - ys), (X - xs))
    
    return psi



def get_velocity_doublet(strength, xd, yd, X, Y):
    """
    From https://nbviewer.org/github/barbagroup/AeroPython/blob/master/lessons/03_Lesson03_doublet.ipynb

    Returns the velocity field generated by a doublet.
    
    Parameters
    ----------
    strength: float
        Strength of the doublet.
    xd: float
        x-coordinate of the doublet.
    yd: float
        y-coordinate of the doublet.
    X: 2D Numpy array of floats
        x-coordinate of the mesh points.
    Y: 2D Numpy array of floats
        y-coordinate of the mesh points.
    
    Returns
    -------
    u: 2D Numpy array of floats
        x-component of the velocity vector field.
    v: 2D Numpy array of floats
        y-component of the velocity vector field.
    """
    u = (- strength / (2 * math.pi) *
         ((X - xd)**2 - (Y - yd)**2) /
         ((X - xd)**2 + (Y - yd)**2)**2)
    v = (- strength / (2 * math.pi) *
         2 * (X - xd) * (Y - yd) /
         ((X - xd)**2 + (Y - yd)**2)**2)
    
    return u, v

def get_stream_function_doublet(strength, xd, yd, X, Y):
    """
    From https://nbviewer.org/github/barbagroup/AeroPython/blob/master/lessons/03_Lesson03_doublet.ipynb

    Returns the stream-function generated by a doublet.
    
    Parameters
    ----------
    strength: float
        Strength of the doublet.
    xd: float
        x-coordinate of the doublet.
    yd: float
        y-coordinate of the doublet.
    X: 2D Numpy array of floats
        x-coordinate of the mesh points.
    Y: 2D Numpy array of floats
        y-coordinate of the mesh points.
    
    Returns
    -------
    psi: 2D Numpy array of floats
        The stream-function.
    """
    psi = - strength / (2 * math.pi) * (Y - yd) / ((X - xd)**2 + (Y - yd)**2)
    
    return psi

def get_cylinder_radius(kappa, u_inf):
    """
    Calculates the radius of the circular cylinder created when
    a doublet of strength kappa is added to a uniform flow
    u_inf.

    Parameters
    ----------
    kappa: float
        Strength of the doublet.
    u_inf: float
        The freestream speed.
    
    Returns
    -------
    radius: float
        The radius of the cylinder.
    """
    return math.sqrt(kappa / (2 * math.pi * u_inf))



def get_stagnation_points(kappa, u_inf):

    """
    From https://nbviewer.org/github/barbagroup/AeroPython/blob/master/lessons/03_Lesson03_doublet.ipynb

    Returns the stagnation points.
    
    Parameters
    ----------
    kappa: float
        Strength of the doublet.
    u_inf: float
        The freestream speed.
    
    Returns
    -------
    x_stagn1: float
        The X-coordinate of the first stagnation point.
    y_stagn1: float
        The Y-coordinate of the first stagnation point.
    x_stagn2: float
        The X-coordinate of the second stagnation point.
    y_stagn2: float
        The Y-coordinate of the second stagnation point.
    """    
    x_stagn1, y_stagn1 = +math.sqrt(kappa / (2 * math.pi * u_inf)), 0.0
    x_stagn2, y_stagn2 = -math.sqrt(kappa / (2 * math.pi * u_inf)), 0.0
    return x_stagn1, y_stagn1, x_stagn2, y_stagn2