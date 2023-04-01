# course-generator
## 概要
#### 特徴
* 簡易的なコース作成を行えるツール
* Bezierなどではなく、コーナー角度や勾配をベースにコースをデザイン出来る

#### 背景
コースをそれっぽく作りたいけど、カーブエディタでやるのもセンスがいるので挫折する人向けにコースエディタを作りました。  
また、コース自動生成などもしやすいような設計にしているので、そういった面でも使う想定があります。

## セットアップ
#### インストール
1. Window > Package ManagerからPackage Managerを開く
2. 「+」ボタン > Add package from git URL
3. 以下を入力してインストール
   * https://github.com/DaitokuAmy/course-generator.git?path=/Packages/com.daitokuamy.coursegenerator
   ![image](https://user-images.githubusercontent.com/6957962/209446846-c9b35922-d8cb-4ba3-961b-52a81515c808.png)

あるいはPackages/manifest.jsonを開き、dependenciesブロックに以下を追記します。

```json
{
    "dependencies": {
        "com.daitokuamy.coursegenerator": "https://github.com/DaitokuAmy/course-generator.git?path=/Packages/com.daitokuamy.coursegenerator"
    }
}
```
バージョンを指定したい場合には以下のように記述します。

https://github.com/DaitokuAmy/course-generator.git?path=/Packages/com.daitokuamy.coursegenerator#1.0.0
