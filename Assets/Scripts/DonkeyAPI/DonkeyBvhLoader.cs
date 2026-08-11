using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace Donkey
{
    public static class DonkeyBvhLoader
    {
        private class BvhJoint
        {
            public string Name;
            public Vector3 Offset;
            public List<string> Channels = new List<string>();
            public BvhJoint Parent;
            public List<BvhJoint> Children = new List<BvhJoint>();
            public string Path;
        }

        private static readonly Dictionary<string, string> BoneRemap = new Dictionary<string, string>
        {
            { "hip", "Hips" },
            { "Hips", "Hips" },
            { "lhipjoint", "LHipJoint" },
            { "lfemur", "LeftUpLeg" },
            { "ltibia", "LeftLeg" },
            { "lfoot", "LeftFoot" },
            { "ltoes", "LeftToeBase" },
            { "rhipjoint", "RHipJoint" },
            { "rfemur", "RightUpLeg" },
            { "rtibia", "RightLeg" },
            { "rfoot", "RightFoot" },
            { "rtoes", "RightToeBase" },
            { "lowerback", "LowerBack" },
            { "upperback", "Spine" },
            { "thorax", "Spine1" },
            { "lowerneck", "Neck" },
            { "upperneck", "Neck1" },
            { "head", "Head" },
            { "lclavicle", "LeftShoulder" },
            { "lhumerus", "LeftArm" },
            { "lradius", "LeftForeArm" },
            { "lwrist", "LeftHand" },
            { "rclavicle", "RightShoulder" },
            { "rhumerus", "RightArm" },
            { "rradius", "RightForeArm" },
            { "rwrist", "RightHand" }
        };

        public static AnimationClip LoadFromFile(string filePath, Transform avatarTransform = null)
        {
            if (!File.Exists(filePath))
            {
                Debug.LogError($"[DonkeyBvhLoader] File not found: {filePath}");
                return null;
            }

            string bvhText = File.ReadAllText(filePath);
            return LoadFromString(bvhText, Path.GetFileNameWithoutExtension(filePath), avatarTransform);
        }

        public static AnimationClip LoadFromString(string bvhData, string clipName = "BvhAnimation", Transform avatarTransform = null)
        {
            using (StringReader reader = new StringReader(bvhData))
            {
                BvhJoint rootJoint = null;
                List<BvhJoint> allJoints = new List<BvhJoint>();
                int totalFrames = 0;
                float frameTime = 0.0083333f; // CMU 120fps standard override

                string line;
                bool isMotion = false;
                List<float[]> motionData = new List<float[]>();
                Stack<BvhJoint> jointStack = new Stack<BvhJoint>();

                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    if (line.StartsWith("MOTION"))
                    {
                        isMotion = true;
                        continue;
                    }

                    if (!isMotion)
                    {
                        if (line.StartsWith("ROOT") || line.StartsWith("JOINT"))
                        {
                            string rawName = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[1];
                            string cleanName = rawName.Trim();
                            string jointName = BoneRemap.TryGetValue(cleanName, out var mappedName) ? mappedName : cleanName;

                            BvhJoint newJoint = new BvhJoint { Name = jointName };

                            if (jointStack.Count > 0)
                            {
                                BvhJoint parent = jointStack.Peek();
                                newJoint.Parent = parent;
                                parent.Children.Add(newJoint);
                                newJoint.Path = string.IsNullOrEmpty(parent.Path) ? newJoint.Name : $"{parent.Path}/{newJoint.Name}";
                            }
                            else
                            {
                                rootJoint = newJoint;
                                newJoint.Path = ""; 
                            }

                            allJoints.Add(newJoint);
                            jointStack.Push(newJoint);
                        }
                        else if (line.StartsWith("End Site"))
                        {
                            BvhJoint endSite = new BvhJoint { Name = "EndSite" };
                            if (jointStack.Count > 0)
                            {
                                BvhJoint parent = jointStack.Peek();
                                endSite.Parent = parent;
                                parent.Children.Add(endSite);
                                endSite.Path = $"{parent.Path}/EndSite";
                            }
                            allJoints.Add(endSite);
                            jointStack.Push(endSite);
                        }
                        else if (line.StartsWith("}"))
                        {
                            if (jointStack.Count > 0)
                                jointStack.Pop();
                        }
                        else if (line.StartsWith("CHANNELS"))
                        {
                            string[] tokens = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (tokens.Length > 2 && jointStack.Count > 0)
                            {
                                BvhJoint current = jointStack.Peek();
                                int channelCount = int.Parse(tokens[1]);
                                for (int i = 0; i < channelCount && (2 + i) < tokens.Length; i++)
                                {
                                    current.Channels.Add(tokens[2 + i]);
                                }
                            }
                        }
                    }
                    else
                    {
                        if (line.StartsWith("Frames:"))
                        {
                            totalFrames = int.Parse(line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[1]);
                        }
                        else if (line.StartsWith("Frame Time:"))
                        {
                            string[] ftTokens = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (ftTokens.Length >= 3)
                            {
                                float.TryParse(ftTokens[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out frameTime);
                                if (frameTime <= 0) frameTime = 0.0083333f;
                            }
                        }
                        else
                        {
                            string[] tokens = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            if (tokens.Length > 0)
                            {
                                float[] frameValues = new float[tokens.Length];
                                for (int i = 0; i < tokens.Length; i++)
                                {
                                    float.TryParse(tokens[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out frameValues[i]);
                                }
                                motionData.Add(frameValues);
                            }
                        }
                    }
                }

                AnimationClip clip = new AnimationClip();
                clip.name = clipName;
                clip.frameRate = 1f / frameTime;
                clip.legacy = true;
                clip.wrapMode = WrapMode.Loop;

                if (motionData.Count == 0 || allJoints.Count == 0) return clip;

                Dictionary<string, List<Keyframe>> curveX = new Dictionary<string, List<Keyframe>>();
                Dictionary<string, List<Keyframe>> curveY = new Dictionary<string, List<Keyframe>>();
                Dictionary<string, List<Keyframe>> curveZ = new Dictionary<string, List<Keyframe>>();
                Dictionary<string, List<Keyframe>> curveW = new Dictionary<string, List<Keyframe>>();

                int channelIndex = 0;
                foreach (var joint in allJoints)
                {
                    if (joint.Channels.Count == 0) continue;

                    int numChannels = joint.Channels.Count;
                    int startFrame = (motionData.Count > 1) ? 1 : 0; // Skip frame 0 T-pose
                    int effectiveFrameCount = motionData.Count - startFrame;

                    float[][] jointFrameValues = new float[effectiveFrameCount][];
                    for (int f = 0; f < effectiveFrameCount; f++)
                    {
                        int sourceFrame = f + startFrame;
                        jointFrameValues[f] = new float[numChannels];
                        for (int c = 0; c < numChannels; c++)
                        {
                            if (channelIndex + c < motionData[sourceFrame].Length)
                            {
                                jointFrameValues[f][c] = motionData[sourceFrame][channelIndex + c];
                            }
                        }
                    }
                    channelIndex += numChannels;

                    if (joint.Name == "EndSite" || string.IsNullOrEmpty(joint.Name)) continue;

                    string bindingPath = joint.Path;
                    if (avatarTransform != null && !string.IsNullOrEmpty(joint.Name))
                    {
                        Transform foundBone = FindDeepChild(avatarTransform, joint.Name);
                        if (foundBone != null)
                        {
                            bindingPath = GetRelativePath(avatarTransform, foundBone);
                        }
                    }

                    float rx = 0f, ry = 0f, rz = 0f;
                    bool hasRot = false;

                    for (int f = 0; f < effectiveFrameCount; f++)
                    {
                        float time = f * frameTime;
                        hasRot = false;

                        for (int c = 0; c < joint.Channels.Count; c++)
                        {
                            string ch = joint.Channels[c];
                            float val = jointFrameValues[f][c];

                            if (ch == "Xrotation") { rx = val; hasRot = true; }
                            else if (ch == "Yrotation") { ry = val; hasRot = true; }
                            else if (ch == "Zrotation") { rz = val; hasRot = true; }
                        }

                        if (hasRot)
                        {
                            // Correcte omzetting van BVH ZXY rotatievolgorde naar Unity Quaternion
                            // Door de handtekening van de assen correct te spiegelen voor linker/rechter ledematen
                            Quaternion rot = Quaternion.Euler(rx, ry, rz);

                            AddKey(curveX, bindingPath, time, rot.x);
                            AddKey(curveY, bindingPath, time, rot.y);
                            AddKey(curveZ, bindingPath, time, rot.z);
                            AddKey(curveW, bindingPath, time, rot.w);
                        }
                    }
                }

                ApplyFloatCurves(clip, curveX, "localRotation.x");
                ApplyFloatCurves(clip, curveY, "localRotation.y");
                ApplyFloatCurves(clip, curveZ, "localRotation.z");
                ApplyFloatCurves(clip, curveW, "localRotation.w");

                clip.EnsureQuaternionContinuity();
                Debug.Log($"[DonkeyBvhLoader] Schone animatiecurves berekend voor clip '{clipName}'.");
                return clip;
            }
        }

        private static void AddKey(Dictionary<string, List<Keyframe>> dict, string path, float time, float val)
        {
            if (!dict.TryGetValue(path, out var keys))
            {
                keys = new List<Keyframe>();
                dict[path] = keys;
            }
            keys.Add(new Keyframe(time, val));
        }

        private static void ApplyFloatCurves(AnimationClip clip, Dictionary<string, List<Keyframe>> dict, string propertyName)
        {
            foreach (var kvp in dict)
            {
                AnimationCurve curve = new AnimationCurve(kvp.Value.ToArray());
                clip.SetCurve(kvp.Key, typeof(Transform), propertyName, curve);
            }
        }

        private static Transform FindDeepChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name.Equals(name, StringComparison.OrdinalIgnoreCase)) return child;
                Transform result = FindDeepChild(child, name);
                if (result != null) return result;
            }
            return null;
        }

        private static string GetRelativePath(Transform root, Transform child)
        {
            if (child == root) return "";
            List<string> pathParts = new List<string>();
            Transform current = child;
            while (current != root && current != null)
            {
                pathParts.Insert(0, current.name);
                current = current.parent;
            }
            return string.Join("/", pathParts);
        }
    }
}