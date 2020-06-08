using MathSupport;
using OpenTK;
using System;
using System.Collections.Generic;
using Utilities;

using Rendering;

namespace DamianWaloszek
{

  /// <summary>
  /// Ray-tracing rendering (w all secondary rays) with abaility to calculate fog.
  /// </summary>
  [Serializable]
  public class RayTracingWithFog : RayTracing
  {
    /// <summary>
    /// Recursive shading function - computes color contribution of the given ray (shot from the
    /// origin 'p0' into direction vector 'p1''). Recursion is stopped
    /// by a hybrid method: 'importance' and 'level' are checked.
    /// Internal integration support.
    /// </summary>
    /// <param name="depth">Current recursion depth.</param>
    /// <param name="importance">Importance of the current ray.</param>
    /// <param name="p0">Ray origin.</param>
    /// <param name="p1">Ray direction vector.</param>
    /// <param name="color">Result color.</param>
    /// <returns>Hash-value (ray sub-signature) used for adaptive subsampling.</returns>
    protected override long shade (int depth,
                                  double importance,
                                  ref Vector3d p0,
                                  ref Vector3d p1,
                                  double[] color)
    {
      Vector3d direction = p1;

      int bands = color.Length;
      LinkedList<Intersection> intersections = scene.Intersectable.Intersect(p0, p1);

      // If the ray is primary, increment both counters
      Statistics.IncrementRaysCounters(1, depth == 0);

      Intersection i = Intersection.FirstIntersection(intersections, ref p1);

      if (i == null)
      {
        // No intersection -> background color
        rayRegisterer?.RegisterRay(AbstractRayRegisterer.RayType.rayVisualizerNormal, depth, p0, direction * 100000);

        return scene.Background.GetColor(p1, color);
      }

      // There was at least one intersection
      i.Complete();

      // Calculating fog contribution if we are in fog
      double[] fogColorContribution = null;
      if (i.Material is UniformFog)
      {
        var next = intersections.Find(i).Next;
        if (next != null)
        {
          Intersection j = next.Value;
          j.Complete();

          fogColorContribution = new double[4]; // RGB + Alpha
          fogColorContribution[0] = i.Material.Color[0];
          fogColorContribution[1] = i.Material.Color[1];
          fogColorContribution[2] = i.Material.Color[2];

          Vector3d start = i.CoordWorld;
          Vector3d end = j.CoordWorld;
          double distance = Vector3d.Distance(start, end);
          // If the distance from the start of the fog to the end of fog/next object
          // is 1 and Fog's transparency value is 0.6, then the alpha channel of fog is 1 - 0.6.
          // If the distance is e.g. 2, then the alpha is 1 - (0.6)^2 etc. - the fog is stronger
          double alphaOfFog = 1 - Math.Pow(i.Material.Kt, distance);
          fogColorContribution[3] = alphaOfFog;

          if (j.Material is UniformFog)
          {
            // i is the start of fog, j the end, so take another intersection
            var next2 = intersections.Find(j).Next;
            if (next2 != null)
            {
              // Set i to intersection after the fog end.
              i = next2.Value;
              i.Complete();
            }
            else
            {
              // No intersection except the fog -> background color + fog
              rayRegisterer?.RegisterRay(AbstractRayRegisterer.RayType.rayVisualizerNormal, depth, p0, direction * 100000);

              // Some hash to return; hash is not modified by going through the fog
              long res = scene.Background.GetColor(p1, color);

              // Apply the fog; if alpha of fog is 0, then pure background color, if 1 then pure fog color
              for (int k = 0; k < 3; ++k)
              {
                color[k] = color[k] * (1 - fogColorContribution[3]) + fogColorContribution[k] * fogColorContribution[3];
              }

              return res;
            }

          }
          else
          {
            // Intersection j is some (solid) object, so assign it to i
            i = j;
          }
        }
      }


      rayRegisterer?.RegisterRay(AbstractRayRegisterer.RayType.unknown, depth, p0, i);

      // Hash code for adaptive supersampling
      long hash = i.Solid.GetHashCode();

      // Apply all the textures first
      if (i.Textures != null)
        foreach (ITexture tex in i.Textures)
          hash = hash * HASH_TEXTURE + tex.Apply(i);

      if (MT.pointCloudCheckBox && !MT.pointCloudSavingInProgress && !MT.singleRayTracing)
      {
        foreach (Intersection intersection in intersections)
        {
          if (!intersection.completed)
            intersection.Complete();

          if (intersection.Textures != null && !intersection.textureApplied)
            foreach (ITexture tex in intersection.Textures)
              tex.Apply(intersection);

          double[] vertexColor = new double[3];
          Util.ColorCopy(intersection.SurfaceColor, vertexColor);
          Master.singleton?.pointCloud?.AddToPointCloud(intersection.CoordWorld, vertexColor, intersection.Normal, MT.threadID);
        }
      }

      // Color accumulation.
      Array.Clear(color, 0, bands);
      double[] comp = new double[bands];

      // Optional override ray-processing (procedural).
      if (DoRecursion &&
          i.Solid?.GetAttribute(PropertyName.RECURSION) is RecursionFunction rf)
      {
        hash += HASH_RECURSION * rf(i, p1, importance, out RayRecursion rr);

        if (rr != null)
        {
          // Direct contribution.
          if (rr.DirectContribution != null &&
              rr.DirectContribution.Length > 0)
            if (rr.DirectContribution.Length == 1)
              Util.ColorAdd(rr.DirectContribution[0], color);
            else
              Util.ColorAdd(rr.DirectContribution, color);

          // Recursive rays.
          if (rr.Rays != null &&
              depth++ < MaxLevel)
            foreach (var ray in rr.Rays)
            {
              RayRecursion.RayContribution rc = ray;
              hash += HASH_REFLECT * shade(depth, rc.importance, ref rc.origin, ref rc.direction, comp);

              // Combine colors.
              if (ray.coefficient == null)
                Util.ColorAdd(comp, color);
              else
              if (ray.coefficient.Length == 1)
                Util.ColorAdd(comp, ray.coefficient[0], color);
              else
                Util.ColorAdd(comp, ray.coefficient, color);
            }

          return hash;
        }
      }

      // Default (Whitted) ray-tracing interaction (lights [+ reflection] [+ refraction]).
      p1 = -p1; // viewing vector
      p1.Normalize();

      if (scene.Sources == null || scene.Sources.Count < 1)
        // No light sources at all.
        Util.ColorAdd(i.SurfaceColor, color);
      else
      {
        // Apply the reflectance model for each source.
        i.Material = (IMaterial)i.Material.Clone();
        i.Material.Color = i.SurfaceColor;

        foreach (ILightSource source in scene.Sources)
        {
          double[] intensity = source.GetIntensity(i, out Vector3d dir);

          if (MT.singleRayTracing && source.position != null)
            // Register shadow ray for RayVisualizer.
            rayRegisterer?.RegisterRay(AbstractRayRegisterer.RayType.rayVisualizerShadow, i.CoordWorld, (Vector3d)source.position);

          if (intensity != null)
          {
            if (DoShadows && dir != Vector3d.Zero)
            {
              intersections = scene.Intersectable.Intersect(i.CoordWorld, dir);
              Statistics.allRaysCount++;
              Intersection si = Intersection.FirstRealIntersection(intersections, ref dir);
              // Better shadow testing: intersection between 0.0 and 1.0 kills the lighting.
              if (si != null && !si.Far(1.0, ref dir))
                continue;
            }

            double[] reflection = i.ReflectanceModel.ColorReflection(i, dir, p1, ReflectionComponent.ALL);
            if (reflection != null)
            {
              Util.ColorAdd(intensity, reflection, color);
              hash = hash * HASH_LIGHT + source.GetHashCode();
            }
          }
        }
      }

      // If we were in fog, apply the fog; if alpha of fog is 0, then pure background color, if 1 then pure fog color
      if (fogColorContribution != null)
      {
        for (int k = 0; k < 3; ++k)
        {
          color[k] = color[k] * (1 - fogColorContribution[3]) + fogColorContribution[k] * fogColorContribution[3];
        }
      }

      // Check the recursion depth. Don't do reflections nor refractions if current intersection is fog.
      if (depth++ >= MaxLevel || (!DoReflections && !DoRefractions) || i.Material is UniformFog)
        // No further recursion.
        return hash;

      Vector3d r;
      double   maxK;
      double   newImportance;

      if (DoReflections)
      {
        // Shooting a reflected ray.
        Geometry.SpecularReflection(ref i.Normal, ref p1, out r);
        double[] ks = i.ReflectanceModel.ColorReflection(i, p1, r, ReflectionComponent.SPECULAR_REFLECTION);
        if (ks != null)
        {
          maxK = ks[0];
          for (int b = 1; b < bands; b++)
            if (ks[b] > maxK)
              maxK = ks[b];

          newImportance = importance * maxK;
          if (newImportance >= MinImportance)
          {
            // Do compute the reflected ray.
            hash += HASH_REFLECT * shade(depth, newImportance, ref i.CoordWorld, ref r, comp);
            Util.ColorAdd(comp, ks, color);
          }
        }
      }

      if (DoRefractions)
      {
        // Shooting a refracted ray.
        maxK = i.Material.Kt;   // simple solution - no influence of reflectance model yet
        newImportance = importance * maxK;
        if (newImportance < MinImportance)
          return hash;

        // Refracted ray.
        if ((r = Geometry.SpecularRefraction(i.Normal, i.Material.n, p1)) == Vector3d.Zero)
          return hash;

        hash += HASH_REFRACT * shade(depth, newImportance, ref i.CoordWorld, ref r, comp);
        Util.ColorAdd(comp, maxK, color);
      }

      return hash;
    }
  }


  /// <summary>
  /// Simple Phong-like reflectance model: material description.
  /// </summary>
  [Serializable]
  public class UniformFog : IMaterial
  {
    /// <summary>
    /// Base surface color.
    /// </summary>
    public double[] Color { get; set; }

    /// <summary>
    /// Coefficient of transparency.
    /// </summary>
    public double Kt { get; set; }

    /// <summary>
    /// Absolute index of refraction.
    /// </summary>
    public double n
    {
      get => 0;
      set { }
    }

    public UniformFog(double[] color, double transparency)
    {
      Color = color;
      Kt = transparency;
    }

    public UniformFog (UniformFog f)
    {
      Color = (double[])f.Color.Clone();
      Kt = f.Kt;
    }

    public UniformFog() : this(new double[] { 0.5, 0.5, 0.5 }, 0.6)
    { }
    

    public object Clone ()
    {
      return new UniformFog(this);
    }
  }

}
