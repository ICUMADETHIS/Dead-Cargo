using UnityEngine;
using CW.Common;
using SpaceGraphicsToolkit.Volumetrics;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using System.Collections.Generic;

namespace SpaceGraphicsToolkit.Ocean
{
	/// <summary>This component can be used to render oceans for your planets.</summary>
	[ExecuteInEditMode]
	[AddComponentMenu("Space Graphics Toolkit/SGT Ocean Cavity")]
	public partial class SgtOceanCavity : SgtVolumeEffect
	{
		/// <summary>The mesh used to define the cavity shape.
		/// NOTE: This mesh must be water tight.</summary>
		public MeshFilter Visual { set { visual = value; } get { return visual; } } [SerializeField] private MeshFilter visual;

		[System.NonSerialized] private Material blitMaterial;

		private static readonly int _SGT_WCam = Shader.PropertyToID("_SGT_WCam");

		[System.NonSerialized] private NativeArray<float3> cachedVertices;
		[System.NonSerialized] private NativeArray<int>    cachedIndices;
		[System.NonSerialized] private Mesh                cachedMesh;
		[System.NonSerialized] private Bounds              cachedBounds;

		public static LinkedList<SgtOceanCavity> AllCavities = new LinkedList<SgtOceanCavity>();

		[System.NonSerialized] private LinkedListNode<SgtOceanCavity> node;

		public override bool IsCavity
		{
			get
			{
				return true;
			}
		}

		public override void CalculateSortDistance(Vector3 worldPoint)
		{
			sortDistance = Vector3.Distance(worldPoint, transform.position);
		}

		public override void RenderCavityBuffer(SgtVolumeManager manager, Camera finalCamera, int pass)
		{
			if (visual != null && visual.sharedMesh != null)
			{
				if (blitMaterial == null)
				{
					blitMaterial = CwHelper.CreateTempMaterial("SgtOceanCavity", "Hidden/SgtOceanCavity");
				}

				blitMaterial.SetVector(_SGT_WCam, finalCamera.transform.position);

				// pass 0 = Front faces, pass 1 = Back faces
				SgtVolumeCamera.AddDrawMesh(visual.sharedMesh, 0, transform.localToWorldMatrix, blitMaterial, pass);
			}
		}

		/// <summary> Checks if a world space point is inside the watertight mesh volume. </summary>
		public static bool IsAnyPointInside(Vector3 worldPoint)
		{
			foreach (var cavity in AllCavities)
			{
				if (cavity.IsPointInside(worldPoint) == true)
				{
					return true;
				}
			}

			return false;
		}

		/// <summary> Checks if a world space point is inside the watertight mesh volume. </summary>
		public bool IsPointInside(Vector3 worldPoint)
		{
			if (visual == null || visual.sharedMesh == null)
				return false;

			if (visual.sharedMesh != cachedMesh)
			{
				UpdateMeshCache(visual.sharedMesh);
			}

			// 1. Fast AABB Check in Local Space
			Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
			if (!cachedBounds.Contains(localPoint))
			{
				return false;
			}

			// 2. Exact Triangle Check via Burst Job
			NativeArray<bool> result = new NativeArray<bool>(1, Allocator.TempJob);
			
			var job = new PointInMeshJob
			{
				Vertices = cachedVertices,
				Indices = cachedIndices,
				LocalPoint = localPoint,
				Result = result
			};

			// Run synchronously on the main thread for immediate result without scheduling overhead
			job.Run();

			bool isInside = result[0];
			result.Dispose();

			return isInside;
		}

		private void UpdateMeshCache(Mesh mesh)
		{
			DisposeCache();

			cachedMesh   = mesh;
			cachedBounds = mesh.bounds;

			Vector3[] verts = mesh.vertices;
			int[] tris = mesh.triangles;

			cachedVertices = new NativeArray<float3>(verts.Length, Allocator.Persistent);
			for (int i = 0; i < verts.Length; i++)
			{
				cachedVertices[i] = verts[i];
			}

			cachedIndices = new NativeArray<int>(tris.Length, Allocator.Persistent);
			for (int i = 0; i < tris.Length; i++)
			{
				cachedIndices[i] = tris[i];
			}
		}

		private void DisposeCache()
		{
			if (cachedVertices.IsCreated) cachedVertices.Dispose();
			if (cachedIndices.IsCreated) cachedIndices.Dispose();

			cachedMesh = null;
		}

		protected override void OnEnable()
		{
			base.OnEnable();

			node = AllCavities.AddLast(this);
		}

		protected override void OnDisable()
		{
			base.OnDisable();

			DisposeCache();

			AllCavities.Remove(node); node = null;
		}

		[BurstCompile]
		private struct PointInMeshJob : IJob
		{
			[ReadOnly] public NativeArray<float3> Vertices;
			[ReadOnly] public NativeArray<int> Indices;
			public float3 LocalPoint;
			
			public NativeArray<bool> Result;

			public void Execute()
			{
				int intersectCount = 0;
				
				// Off-axis ray direction to minimize exact edge/vertex intersections
				float3 rayDir = math.normalize(new float3(0.31f, 0.72f, 0.54f));

				for (int i = 0; i < Indices.Length; i += 3)
				{
					float3 v0 = Vertices[Indices[i]];
					float3 v1 = Vertices[Indices[i + 1]];
					float3 v2 = Vertices[Indices[i + 2]];

					if (RayIntersectsTriangle(LocalPoint, rayDir, v0, v1, v2))
					{
						intersectCount++;
					}
				}

				// Odd number of intersections means the point is inside the watertight volume
				Result[0] = (intersectCount % 2) != 0;
			}

			private bool RayIntersectsTriangle(float3 rayOrigin, float3 rayVector, float3 vertex0, float3 vertex1, float3 vertex2)
			{
				const float EPSILON = 0.0000001f;
				float3 edge1 = vertex1 - vertex0;
				float3 edge2 = vertex2 - vertex0;
				
				float3 h = math.cross(rayVector, edge2);
				float a = math.dot(edge1, h);

				if (a > -EPSILON && a < EPSILON)
					return false; 

				float f = 1.0f / a;
				float3 s = rayOrigin - vertex0;
				float u = f * math.dot(s, h);
				
				if (u < 0.0f || u > 1.0f)
					return false;

				float3 q = math.cross(s, edge1);
				float v = f * math.dot(rayVector, q);
				
				if (v < 0.0f || u + v > 1.0f)
					return false;

				float t = f * math.dot(edge2, q);
				
				if (t > EPSILON)
					return true; 

				return false;
			}
		}
	}
}

#if UNITY_EDITOR
namespace SpaceGraphicsToolkit.Ocean
{
	[UnityEditor.CanEditMultipleObjects]
	[UnityEditor.CustomEditor(typeof(SgtOceanCavity))]
	public class SgtOceanCavity_Editor : CwEditor
	{
		protected override void OnInspector()
		{
			SgtOceanCavity tgt; SgtOceanCavity[] tgts; GetTargets(out tgt, out tgts);

			BeginError(Any(tgts, t => t.Visual == null));
				Draw("visual", "The mesh used to define the cavity shape.\n\nNOTE: This mesh must be water tight.");
			EndError();
		}
	}
}
#endif