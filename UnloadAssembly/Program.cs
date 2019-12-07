using System;
using System.Reflection;
using System.Runtime.Loader;
using System.IO;
using System.Runtime.CompilerServices;

namespace UnloadAssembly
{
    class TestAssemblyLoadContext : AssemblyLoadContext
    {
        public TestAssemblyLoadContext() : base(isCollectible: true)
        {
        }
    }

    class Program
    {
        // インライン化されないように
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ExecuteAndUnload(string assemblyPath, out WeakReference alcWeakRef)
        {
            // アセンブリをロードするAssemblyLoadContextを作成
            var alc = new TestAssemblyLoadContext();

            // アセンブリをロード
            Assembly a = alc.LoadFromAssemblyPath(assemblyPath);

            // 外からアンロードを検知するために弱参照を設定
            alcWeakRef = new WeakReference(alc, trackResurrection: true);

            // リフレクションで関数コール
            var type = a.GetType("ClassLibrary1.Class1");
            var instance = Activator.CreateInstance(type);
            var helloMethod = type.GetMethod("Hello");
            helloMethod.Invoke(instance, new object[] { 1 });

            // アンロード実施
            alc.Unload();
        }

        static void Main(string[] args)
        {
            var myDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            // 読み込むアセンブリ(dll)のパス
            var assemblyPath = Path.Combine(myDirectory, @"..\..\..\..\ClassLibrary1\bin\Debug\netstandard2.1\ClassLibrary1.dll");

            // アセンブリを読み込んで関数をコール
            ExecuteAndUnload(assemblyPath, out WeakReference alcWeakRef);

            try
            {
                File.Delete(assemblyPath);
            }
            catch(UnauthorizedAccessException)
            {
                Console.WriteLine("アンロード完了してないので消せない");
            }

            // アンロードされるまで待つ
            int counter = 0;
            for (counter = 0; alcWeakRef.IsAlive && (counter < 10); counter++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            
            if (counter < 10)
            {
                // この段階ではアンロード済みなので消せる
                File.Delete(assemblyPath);
                Console.WriteLine("アンロード成功");
            }
            else
            {
                Console.WriteLine("アンロード失敗");
            }

            Console.ReadKey();
        }
    }
}
