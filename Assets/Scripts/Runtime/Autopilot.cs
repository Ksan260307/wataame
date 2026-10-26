using System.Collections;
using UnityEngine;

namespace Wataame
{
    /// <summary>
    /// 動作確認用の自動操縦。コマンドライン引数で起動したときだけ動く。
    ///   -wataame-autopilot            ざらめを入れてスイッチを入れ、棒を回してわたあめを作る
    ///   -wataame-screenshot &lt;dir&gt;    途中と完成時のスクリーンショットを保存して終了する
    /// </summary>
    public sealed class Autopilot : MonoBehaviour
    {
        WataameGame game;
        string screenshotDir;

        public static void AttachIfRequested(WataameGame game)
        {
            var args = System.Environment.GetCommandLineArgs();
            if (System.Array.IndexOf(args, "-wataame-autopilot") < 0) return;

            var pilot = game.gameObject.AddComponent<Autopilot>();
            pilot.game = game;
            int i = System.Array.IndexOf(args, "-wataame-screenshot");
            if (i >= 0 && i + 1 < args.Length) pilot.screenshotDir = args[i + 1];
        }

        IEnumerator Start()
        {
            yield return new WaitForSeconds(0.5f);
            game.ColorIndex = 0;
            game.SugarAmount = 14f;
            game.PourSugar();
            yield return new WaitForSeconds(0.8f);
            game.ColorIndex = 1;
            game.SugarAmount = 8f;
            game.PourSugar();
            yield return new WaitForSeconds(0.8f);
            game.ToggleSwitch();
            game.Stick.AutoSpin = true;

            float t = 0f;
            bool midShot = false;
            while (!game.Machine.Hopper.IsEmpty && t < 40f)
            {
                t += Time.deltaTime;
                // ボウルの中をゆっくり大きく回る
                float a = t * 0.5f;
                var target = new Vector3(Mathf.Cos(a) * 0.17f, 0.25f + 0.04f * Mathf.Sin(t * 0.8f), Mathf.Sin(a) * 0.17f);
                game.Stick.SetTarget(target);
                if (!midShot && t > 8f)
                {
                    midShot = true;
                    Capture("making.png");
                }
                yield return null;
            }

            game.ToggleSwitch();
            game.Finish();
            var r = game.Result;
            Debug.Log($"[Wataame] Autopilot result: {r.Name} {r.Grams:0.0}g {r.WidthCm:0}x{r.HeightCm:0}cm round={r.Roundness:0.00} stars={r.Stars} catch={game.Candy.Field.TotalGrams / 22f:P0}");
            yield return new WaitForSeconds(3f);
            Capture("finished.png");

            if (!string.IsNullOrEmpty(screenshotDir))
            {
                yield return new WaitForSeconds(1f);
                Application.Quit();
            }
        }

        void Capture(string file)
        {
            if (string.IsNullOrEmpty(screenshotDir)) return;
            System.IO.Directory.CreateDirectory(screenshotDir);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(screenshotDir, file));
        }
    }
}
