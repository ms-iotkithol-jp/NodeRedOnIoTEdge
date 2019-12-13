# Node-Red の Node を追加したい場合 
1. 追加したい Node のソース一式を探す。
2. VS Code + Azure IoT Edge Extension で、新しくSolutionを作る。 
3. Node.js テンプレートを使って、IoT Edge モジュールを追加する。 
4. 追加してできた modules の下の 追加モジュール名のディレクトリの、package.json、app.jsを削除する。
5. https://github.com/iotblackbelt/noderededgemodule を submodule として追加する 
6. [TibboPiOnIoTEdgeModule/DOckerfile.arm32v7](../samples/TibboPiOnIoTEdge/modules/TibboPiOnIoTEdgeModule/Dockerfile.arm32v7) で上書きする 
7. Dockerfile.arm32v7 の node-red-contrib-tibbo-pi-p の行を、追加した ノードに合わせて変える。 

以上