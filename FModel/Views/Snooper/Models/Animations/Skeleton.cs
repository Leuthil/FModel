﻿using System;
using System.Collections.Generic;
using System.Numerics;
using CUE4Parse_Conversion.Animations;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.UObject;
using FModel.Views.Snooper.Shading;
using Serilog;

namespace FModel.Views.Snooper.Models.Animations;

public class Skeleton : IDisposable
{
    public readonly USkeleton UnrealSkeleton;
    public readonly Dictionary<string, int> BonesIndexByName;
    public readonly Dictionary<int, Transform> BonesTransformByIndex;
    public readonly bool IsLoaded;

    public Animation Anim;

    public Skeleton()
    {
        BonesIndexByName = new Dictionary<string, int>();
        BonesTransformByIndex = new Dictionary<int, Transform>();
    }

    public Skeleton(FPackageIndex package, Transform transform) : this()
    {
        UnrealSkeleton = package.Load<USkeleton>();
        if (UnrealSkeleton == null) return;

        BonesIndexByName = UnrealSkeleton.ReferenceSkeleton.FinalNameToIndexMap;
        BonesTransformByIndex = new Dictionary<int, Transform>();
        foreach ((_, int boneIndex) in BonesIndexByName)
        {
            var transforms = new List<Transform>();
            var parentBoneIndex = boneIndex;
            while (parentBoneIndex > -1)
            {
                var parentFound = BonesTransformByIndex.TryGetValue(parentBoneIndex, out var boneTransform);
                if (!parentFound)
                {
                    var bone = UnrealSkeleton.ReferenceSkeleton.FinalRefBonePose[parentBoneIndex];
                    boneTransform = new Transform
                    {
                        Relation = transform.Matrix,
                        Rotation = bone.Rotation,
                        Position = bone.Translation * Constants.SCALE_DOWN_RATIO,
                        Scale = bone.Scale3D
                    };
                }

                parentBoneIndex = UnrealSkeleton.ReferenceSkeleton.FinalRefBoneInfo[parentBoneIndex].ParentIndex;
                transforms.Add(boneTransform);
                if (parentFound) parentBoneIndex = -1; // the parent transform is already relative to all its parent so we can just skip
            }

            for (int j = transforms.Count - 2; j > -1; j--)
            {
                transforms[j].Relation *= transforms[j + 1].Matrix;
            }

            BonesTransformByIndex[boneIndex] = transforms[0];
            transforms.Clear();
        }
        IsLoaded = true;
    }

    public void SetAnimation(CAnimSet anim)
    {
        Anim = new Animation(anim, BonesIndexByName, BonesTransformByIndex);
    }

    public void UpdateRootBoneMatrix(Matrix4x4 delta)
    {
        // Matrix4x4.Decompose(delta, out var scale, out var rotation, out var position);
        // Log.Logger.Information("Update");

        // TODO: support for rotation and scale
        BonesTransformByIndex[0].Relation.Translation += delta.Translation;
    }

    public void SetUniform(Shader shader)
    {
        if (!IsLoaded) return;
        for (var i = 0; i < Anim?.FinalBonesMatrix.Length; i++)
        {
            shader.SetUniform($"uFinalBonesMatrix[{i}]", Anim.FinalBonesMatrix[i].Matrix);
        }
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}