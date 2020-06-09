# Extension: UniformFog

### Author: Damian Wałoszek
### Category: Material / RayTracing algorithm
### Namespace: DamianWaloszek
### ClassNames: RayTracingWithFog (extends RayTracing), UniformFog (implements IMaterial)
### ITimeDependent: No
### Source file: UniformFog.cs

## Description

The extension creates a material (with the modified RayTracing technique) with fog/cloud-like properties - if the fog is between camera and object, the object will be colored with fog color with the intensity dependent on how much (absolute) distance in the ray's path was in fog.

Rays passes through the fog, so no reflections nor refractions are computed for UniformFog, however is can be seen on reflections/refractions of other objects. UniformFog also casts shadow (like the real clouds do), but I guess that you can disable shadow casting for particular objects with UniformFog material.

Currently, the extension doesn't work for objects inside the fog (because of bad implementation of program's build-in Intersect funtion), but should be fixed in future. <br>
Also, setting `context[PropertyName.CTX_ALGORITHM] = new RayTracingWithFog();` is currently broken, so we have to wait for fix (workaround: copy content of shade() function to the RayTracing class or set this in preprocessing).

<p align="center">
 <img src="/Screenshots/1.png">
</p>

## Usage

Example usage is in file TwoSpheresAndFog.cs (don't forget to move it to /data/rtscenes), you set ray tracing algorithm: <br>
`context[PropertyName.CTX_ALGORITHM] = new RayTracingWithFog();` <br>
so the ray traces knows how to handle clouds. (currently broken as mentioned above) <br>

Then you create a new UniformFog instance like this: <br>
`UniformFog fog = new UniformFog(new double[] { 0.5, 0.5, 0.5 }, 0.6);` <br>
where the first argument is a double array of length 3 containing RGB values (in interval [0; 1]) of fog color, and the second value is a double that contains info about how much transparent the fog is (from 0 - not transnparent at all, to 1 - 100% transparent). <br>

And then you can create some Solid objects (I recommend making a scene where every ray would have max. 2 intersections with fog (e.g. one fog sphere) - the ray currently can't pass through more than 2 intersections), and set the fog as their MATERIAL property like this: <br>
`mySolid.SetAttribute(PropertyName.MATERIAL, fog);` <br>
If you want, you can also set `NO_SHADOW` attribute for the object like this: <br>
`mySolid.SetAttribute(PropertyName.NO_SHADOW, anyNonNullObject);` <br>
