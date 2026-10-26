// ===========================================================
// Assets/SquashGame/Editor/MusicImport.cs
// Batch 6: the songs in Resources/Music stream from disk (a few seconds
// in memory, not minutes), Vorbis-compressed - an hour of music would
// otherwise be hundreds of MB in memory and in the build.
// ===========================================================

using UnityEditor;
using UnityEngine;

namespace SquashGame.Editor
{
    public sealed class MusicImport : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.Replace("\\", "/").Contains("/Resources/Music/")) return;
            var imp = (AudioImporter)assetImporter;
            var s = imp.defaultSampleSettings;
            s.loadType = AudioClipLoadType.Streaming;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.45f;
            s.preloadAudioData = false;
            imp.defaultSampleSettings = s;
            imp.loadInBackground = true;
        }
    }
}
