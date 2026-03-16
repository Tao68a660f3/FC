using System;
using System.IO;
using System.Threading.Tasks;

namespace FC.Core
{
    internal class GeneratorEngine
    {
        private FontRender _renderer;

        public GeneratorEngine(FontRender renderer)
        {
            _renderer = renderer;
        }

        /// <summary>
        /// 执行非ASCII字库生成循环
        /// </summary>
        /// <param name="provider">编码模式提供者 (GBK_Custom 或 GB2312)</param>
        /// <param name="outputPath">输出 .bin 路径</param>
        /// <param name="progressCallback">进度回调 (已完成数, 总数)</param>
        public async Task GenerateAsync(IEncodingProvider provider, string outputPath, Action<int, int> progressCallback)
        {
            await Task.Run(() =>
            {
                // 1. 获取所有编码流并转为 List 以便统计总数
                var encodingList = new System.Collections.Generic.List<ushort>(provider.GetEncodingStream());
                int total = encodingList.Count;
                int current = 0;

                using (var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                {
                    foreach (var code in encodingList)
                    {
                        // 2. 将编码转为字符字符串
                        string text = provider.GetString(code);

                        // 3. 渲染并取模 (使用你刚才设置好的 ScanMode 和 BitOrder)
                        byte[] glyphData = _renderer.RenderChar(text);

                        // 4. 写入文件
                        fs.Write(glyphData, 0, glyphData.Length);

                        // 5. 更新进度
                        current++;
                        if (current % 100 == 0 || current == total) // 每100个字符回调一次，减轻UI压力
                        {
                            progressCallback?.Invoke(current, total);
                        }
                    }
                }
            });
        }
    }
}