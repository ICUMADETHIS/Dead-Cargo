using Unity.Mathematics;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using System.Collections.Generic;

namespace SpaceGraphicsToolkit.Landscape
{
	/// <summary>This component snaps a spawned cave prefab to the surface of a planet and modifies its meshes to form a cave interior with multiple entrances using background jobs.</summary>
	[AddComponentMenu("Space Graphics Toolkit/SGT Landscape Cave")]
	public class SgtLandscapeCave : MonoBehaviour
	{
		/// <summary>The prefab component that will be spawned at the cave location.</summary>
		public SgtLandscapeCaveData Prefab { set { prefab = value; } get { return prefab; } } [SerializeField] private SgtLandscapeCaveData prefab;

		[System.NonSerialized] private SgtSphereLandscape parent;

		[System.NonSerialized] private SgtSphereLandscape.PendingPoints pendingData;

		[System.NonSerialized] private JobHandle pendingHandle;

		[System.NonSerialized] private bool started;

		[System.NonSerialized] private bool isGenerating;

		[System.NonSerialized] private NativeList<double3> spawnPoints;

		[System.NonSerialized] private List<MeshState> meshStates = new List<MeshState>();

		[System.NonSerialized] private List<TransformState> childStates = new List<TransformState>();

		[System.NonSerialized] private double3    anchorPosition;
		[System.NonSerialized] private quaternion anchorRotation;
		[System.NonSerialized] private float3     anchorScale;

		public SgtLandscapeCaveData SpawnedInstance { get { CheckState(); return spawnedInstance; } } [System.NonSerialized] private SgtLandscapeCaveData spawnedInstance;

		private class MeshState
		{
			public MeshFilter Filter;
			public Mesh       OriginalMesh;
			public Vector3[]  OriginalVertices;
			public int        StartIndex;
			public Matrix4x4  OriginalMeshToRoot;
		}

		private class TransformState
		{
			public int     JobIndex;
			public Vector3 OriginalLocalPosition;
		}

		public bool IsReady
		{
			get
			{
				CheckState();

				return started == true && isGenerating == false;
			}
		}

		public void CheckState()
		{
			if (started == false) GenerateNow();

			if (isGenerating == true)
			{
				if (pendingHandle.IsCompleted == true)
				{
					CompleteGeneration();
				}
			}
		}

		protected virtual void OnEnable()
		{
			parent = GetComponentInParent<SgtSphereLandscape>();

			if (parent != null)
			{
				parent.RegisterCave(this);
				parent.OnDispose += HandleDispose;
			}
		}

		protected virtual void OnDisable()
		{
			CleanupGeneration();

			if (parent != null)
			{
				parent.UnregisterCave(this);
				parent.OnDispose -= HandleDispose;
			}
		}

		private void HandleDispose()
		{
			CleanupGeneration();
		}

		private void CleanupGeneration()
		{
			if (isGenerating == true)
			{
				pendingHandle.Complete();
				pendingData.Dispose();
				isGenerating = false;
			}

			if (spawnPoints.IsCreated == true)
			{
				spawnPoints.Dispose();
			}

			if (spawnedInstance != null)
			{
				foreach (var state in meshStates)
				{
					if (state.Filter != null)
					{
						var currentMesh = state.Filter.sharedMesh;
						if (currentMesh != state.OriginalMesh && currentMesh != null && currentMesh.name.EndsWith(" (Cave Instance)"))
						{
							state.Filter.sharedMesh = state.OriginalMesh;
							
							if (Application.isPlaying == true)
							{
								Destroy(currentMesh);
							}
							else
							{
								DestroyImmediate(currentMesh);
							}
						}

						var mc = state.Filter.GetComponent<MeshCollider>();
						if (mc != null && mc.sharedMesh != state.OriginalMesh && mc.sharedMesh != null && mc.sharedMesh.name.EndsWith(" (Cave Instance)"))
						{
							mc.sharedMesh = state.OriginalMesh;
						}
					}
				}

				if (Application.isPlaying == true)
				{
					Destroy(spawnedInstance.gameObject);
				}
				else
				{
					DestroyImmediate(spawnedInstance.gameObject);
				}

				spawnedInstance = null;
			}

			started = false;
		}

		private static double4x4 BuildEntranceProjection(double3 localPos, double3 right, double3 up, double3 forward, double3 localSize)
		{
			var invSize = 1.0 / localSize;

			right   *= invSize.x;
			up      *= invSize.y;
			forward *= invSize.z;

			return new double4x4(
				new double4(right.x,   up.x,   forward.x, 0),
				new double4(right.y,   up.y,   forward.y, 0),
				new double4(right.z,   up.z,   forward.z, 0),
				new double4(
					-math.dot(right,   localPos),
					-math.dot(up,      localPos),
					-math.dot(forward, localPos),
					1
				)
			);
		}

		public void GenerateNow()
		{
			if (parent != null && parent.IsActivated == true && prefab != null)
			{
				started      = true;
				isGenerating = true;

				spawnedInstance = Instantiate(prefab);

				var localPoint      = new double3(transform.localPosition);
				var surfacePoint    = parent.GetLocalPoint(localPoint);
				var surfaceUp       = math.normalize(parent.GetLocalDirection(localPoint));
				var surfaceRotation = quaternion.LookRotationSafe(transform.forward, (float3)surfaceUp);

				anchorPosition = surfacePoint;
				anchorRotation = surfaceRotation;
				anchorScale    = transform.localScale;

				transform.localPosition = (float3)surfacePoint;
				transform.localRotation = surfaceRotation;

				if (spawnedInstance.Entrances != null)
				{
					foreach (var entrance in spawnedInstance.Entrances)
					{
						if (entrance == null) continue;

						var entRotation = math.mul(anchorRotation, quaternion.Euler(math.radians(entrance.Rotation)));
						var entPosition = anchorPosition + math.rotate(anchorRotation, entrance.Position * anchorScale);

						var right   = math.mul(entRotation, new float3(1, 0, 0));
						var up      = math.mul(entRotation, new float3(0, 1, 0));
						var forward = math.mul(entRotation, new float3(0, 0, 1));

						entrance.EntranceMatrix = BuildEntranceProjection(entPosition, right, up, forward, new double3(entrance.Size * entrance.Radius));
					}
				}

				meshStates.Clear();
				childStates.Clear();
				
				var totalVertices = 0;

				foreach (var mf in spawnedInstance.DeformVisuals)
				{
					if (mf != null && mf.sharedMesh != null)
					{
						var mesh = mf.sharedMesh;
						var meshToRoot = spawnedInstance.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;

						meshStates.Add(new MeshState 
						{ 
							Filter = mf, 
							OriginalMesh = mesh, 
							OriginalVertices = mesh.vertices, 
							StartIndex = totalVertices, 
							OriginalMeshToRoot = meshToRoot 
						});
						totalVertices += mesh.vertexCount;
					}
				}

				var childCount = spawnedInstance.transform.childCount;
				spawnPoints = new NativeList<double3>(childCount + totalVertices, Allocator.Persistent);

				for (var i = 0; i < childCount; i++)
				{
					var child = spawnedInstance.transform.GetChild(i);
					childStates.Add(new TransformState { JobIndex = spawnPoints.Length, OriginalLocalPosition = child.localPosition });
					
					var worldLocalPos = anchorPosition + (double3)math.rotate(anchorRotation, (float3)(anchorScale * (float3)child.localPosition));
					spawnPoints.Add(worldLocalPos);
				}

				foreach (var state in meshStates)
				{
					for (var i = 0; i < state.OriginalVertices.Length; i++)
					{
						var rootLocalPos = state.OriginalMeshToRoot.MultiplyPoint3x4(state.OriginalVertices[i]);
						var worldLocalPos = anchorPosition + (double3)math.rotate(anchorRotation, (float3)(anchorScale * (float3)rootLocalPos));
						spawnPoints.Add(worldLocalPos);
					}
				}

				pendingData   = parent.SchedulePoints(spawnPoints);
				pendingHandle = pendingData.Handle;
			}
		}

		private void CompleteGeneration()
		{
			isGenerating = false;
			pendingHandle.Complete();

			if (spawnedInstance != null)
			{
				parent.AddChild(spawnedInstance.transform, anchorPosition, anchorRotation, anchorScale);

				var invRotation = math.inverse(anchorRotation);
				var invScale    = 1.0f / anchorScale;

				for (var i = 0; i < childStates.Count; i++)
				{
					var state = childStates[i];
					var terrainPos = pendingData.Points[state.JobIndex] + pendingData.Directions[state.JobIndex] * pendingData.Heights[state.JobIndex];
					
					var localTerrainPos = math.rotate(invRotation, (float3)(terrainPos - anchorPosition)) * invScale;
					
					var child = spawnedInstance.transform.GetChild(i);
					var newPos = state.OriginalLocalPosition;
					newPos.y += localTerrainPos.y;
					child.localPosition = newPos;
				}

				foreach (var state in meshStates)
				{
					if (state.Filter == null) continue;

					var modifiedVertices = new Vector3[state.OriginalVertices.Length];
					var newRootToMesh = state.Filter.transform.worldToLocalMatrix * spawnedInstance.transform.localToWorldMatrix;

					for (var i = 0; i < state.OriginalVertices.Length; i++)
					{
						var jobIndex = childStates.Count + state.StartIndex + i;
						var terrainPos = pendingData.Points[jobIndex] + pendingData.Directions[jobIndex] * pendingData.Heights[jobIndex];
						
						var localTerrainPos = math.rotate(invRotation, (float3)(terrainPos - anchorPosition)) * invScale;

						var rootLocalPos = state.OriginalMeshToRoot.MultiplyPoint3x4(state.OriginalVertices[i]);
						rootLocalPos.y += localTerrainPos.y;

						modifiedVertices[i] = newRootToMesh.MultiplyPoint3x4(rootLocalPos);
					}

					var newMesh = Instantiate(state.OriginalMesh);
					newMesh.name     = state.OriginalMesh.name + " (Cave Instance)";
					newMesh.vertices = modifiedVertices;
					newMesh.RecalculateBounds();

					state.Filter.sharedMesh = newMesh;

					var mc = state.Filter.GetComponent<MeshCollider>();
					if (mc != null)
					{
						mc.sharedMesh = newMesh;
					}
				}
			}

			pendingData.Dispose();
			if (spawnPoints.IsCreated) spawnPoints.Dispose();
		}

#if UNITY_EDITOR
		protected virtual void OnDrawGizmosSelected()
		{
			if (prefab == null || prefab.Entrances == null) return;

			Gizmos.color = Color.cyan;
			foreach (var entrance in prefab.Entrances)
			{
				if (entrance == null) continue;

				Matrix4x4 localMatrix = Matrix4x4.TRS(entrance.Position, Quaternion.Euler(entrance.Rotation), entrance.Size * entrance.Radius);
				Gizmos.matrix = transform.localToWorldMatrix * localMatrix;

				if (entrance.Shape == SgtLandscapeCaveData.ShapeType.Circle) Gizmos.DrawWireSphere(Vector3.zero, 1.0f);
				if (entrance.Shape == SgtLandscapeCaveData.ShapeType.Box)    Gizmos.DrawWireCube(Vector3.zero, Vector3.one * 2.0f);
			}
		}
#endif
	}
}

#if UNITY_EDITOR
namespace SpaceGraphicsToolkit.Landscape
{
	[UnityEditor.CanEditMultipleObjects]
	[UnityEditor.CustomEditor(typeof(SgtLandscapeCave))]
	public class SgtLandscapeCave_Editor : CW.Common.CwEditor
	{
		protected override void OnInspector()
		{
			SgtLandscapeCave tgt; SgtLandscapeCave[] tgts; GetTargets(out tgt, out tgts);

			Draw("prefab", "The prefab component that will be spawned at the cave location.");
		}
	}
}
#endif