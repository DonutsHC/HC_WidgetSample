# How to Use
`A.` Either clone this repository to inspect the whole Unity project sample, or 

`B.` Use **WidgetScriptsGenerator** package:
1. In **Unity - Package Manager**, click the **"+"** button to access **"Install package from git URL..."**
2. Enter
```
https://github.com/DonutsHC/HC_WidgetSample.git?path=Assets/WidgetScriptsGenerator
```
4. In the top menu bar, access **Tools - Widget Generator**
5. Fill the fields accordingly, and generate the required files
6. ⚠️ Don't forget to change the placeholder images, font, or assets in `Assets/Plugins/Android/GameWidget.androidlib/res`
7. ⚠️ Don't forget to add `WidgetDataWriter` component (or your own script) to the scene, so the widget's save data can be generated once the app is opened

___

# 使用方法
`A.` このリポジトリをクローンしてUnityプロジェクトのサンプル全体を確認するか、

`B.` **WidgetScriptsGenerator** パッケージを使用します：

1. **Unity - Package Manager** を開き、「**+**」ボタンをクリックして **"Install package from git URL..."** を選択します。
2. 以下を入力します：
```
https://github.com/DonutsHC/HC_WidgetSample.git?path=Assets/WidgetScriptsGenerator
上部のメニューバーから Tools - Widget Generator を選択します。
```
3. 各フィールドに必要事項を入力し、必要なファイルを生成します。
4. ⚠️ `Assets/Plugins/Android/GameWidget.androidlib/res` 内にあるプレースホルダーの画像、フォント、アセットを忘れずに変更してください。
5. ⚠️ アプリを開いた際にウィジェットのセーブデータが生成されるよう、WidgetDataWriter コンポーネント（または独自のスクリプト）をシーンに追加するのを忘れないでください。
