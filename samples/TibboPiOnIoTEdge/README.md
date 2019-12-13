# [Tibbo-Pi](https://tibbo-pi.co-works.co.jp/) × [Azure IoT Edge](https://docs.microsoft.com/ja-jp/azure/iot-edge/) 
このモジュールは、embeddedgeorge/tibbopi-iot-edge-module:0.2.1-arm32v7 から利用可能。 

## 使用方法 
1. Raspberry Pi(Tibbo-Pi)のマイクロSDカードを、RaspbianのStrechで初期化し、セットアップする 
2. Azure IoT Edge Runtime をインストールする。インストール方法は[こちら](https://docs.microsoft.com/ja-jp/azure/iot-edge/how-to-install-iot-edge-linux) 
3. IoT Edge Module の Build と 配置 - 以下を参照 

### Build と配置 
#### Docker に詳しい人向け 
ラズパイに、このディレクトリ以下をコピーし、以下を実行  
$ sudo docker build -t tibbopi-iot-edge-module -f Dockerfile.arm32v7 .  
$ sudo docker build tag tibbopi-iot-edge-module <i>your-docker-repository</i>/tibbopi-iot-edge-module  
$ docker push <i>your-docker-repository</i>/tibbopi-iot-edge-module:latest  
これで、Azure IoT Hub、IoT Edge デバイスから使えるようになるので、Azure ポータルなどで、IoT Edge デバイスに配置する。 
[Blob on Edge](https://docs.microsoft.com/ja-jp/azure/iot-edge/how-to-access-host-storage-from-module) と [file-sync-helper](../../helper/NodeRedHelperModules) を併用すると、Node-Red のフローをリモートで収集・更新が可能になる。 

#### VS Code + Azure IoT Edge Extension を使う場合 
※ 書きかけ 
