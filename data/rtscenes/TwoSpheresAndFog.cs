//////////////////////////////////////////////////
///Externals.

using DamianWaloszek;

//////////////////////////////////////////////////
// Rendering params.

Debug.Assert(scene != null);
Debug.Assert(context != null);

// Set Ray Tracing algorith to the one that can handle fog.
// If it's using normal RayTracing class, then it's because of a bug,
// and you have to replace content of RayTracing.shade() function
// with the content of RayTracingWithFog.shade().
context[PropertyName.CTX_ALGORITHM] = new RayTracingWithFog();

// Tooltip.
context[PropertyName.CTX_TOOLTIP] = "r=<double> g=<double> b=<double> t=<double>\r(R, G, B, transparency of fog, all values in [0, 1])";

// Params dictionary.
Dictionary<string, string> p = Util.ParseKeyValueList(param);

// Parsing params.
double r = 0.5;
Util.TryParse(p, "r", ref r);

double g = 0.5;
Util.TryParse(p, "g", ref g);

double b = 0.5;
Util.TryParse(p, "b", ref b);

double t = 0.6;
Util.TryParse(p, "t", ref t);

//////////////////////////////////////////////////
// CSG scene.

CSGInnerNode root = new CSGInnerNode(SetOperation.Union);
root.SetAttribute(PropertyName.REFLECTANCE_MODEL, new PhongModel());
root.SetAttribute(PropertyName.MATERIAL, new PhongMaterial(new double[] {1.0, 0.7, 0.1}, 0.1, 0.7, 0.3, 128, 1.0));
scene.Intersectable = root;

// Background color.
scene.BackgroundColor = new double[] {0.0, 0.01, 0.03};
scene.Background = new DefaultBackground(scene.BackgroundColor);

// Camera.
scene.Camera = new StaticCamera(new Vector3d(0.0, 0.5, -5.0),
                                new Vector3d(0.0, -0.18, 1.0),
                                70.0);

// Light sources.
scene.Sources = new System.Collections.Generic.LinkedList<ILightSource>();
scene.Sources.Add(new AmbientLightSource(0.8));
scene.Sources.Add(new PointLightSource(new Vector3d(-5.0, 4.0, -3.0), 1.2));

// --- NODE DEFINITIONS ----------------------------------------------------

// Fog Sphere.
UniformFog fog = new UniformFog(new double[] { r, g, b }, t);
Sphere s;
s = new Sphere();
s.SetAttribute(PropertyName.MATERIAL, fog);
root.InsertChild(s, Matrix4d.Scale(2) * Matrix4d.CreateTranslation(0, 1, 3));

// Sphere that is partially inside the fog. The part inside isn't rendered
// due to the wrong implementation of Intersect funciton.
s = new Sphere();
root.InsertChild(s, Matrix4d.Scale(1.2) * Matrix4d.CreateTranslation(1, 0.5, 3));

// Normal sphere behind the fog.
s = new Sphere();
root.InsertChild(s, Matrix4d.Scale(1.2) * Matrix4d.CreateTranslation(-2, 1.5, 8));

// Infinite plane with checker.
Plane pl = new Plane();
pl.SetAttribute(PropertyName.COLOR, new double[] {0.3, 0.0, 0.0});
pl.SetAttribute(PropertyName.TEXTURE, new CheckerTexture(0.6, 0.6, new double[] {1.0, 1.0, 1.0}));
root.InsertChild(pl, Matrix4d.RotateX(-MathHelper.PiOver2) * Matrix4d.CreateTranslation(0.0, -1.0, 0.0));
