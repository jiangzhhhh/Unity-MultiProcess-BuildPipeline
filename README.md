# Unity-MultiProcess-BuildPipeline
多进程资源构建方案

# 设计
- Windows借助第三方软件junction，通过符号链接Assets/ProjectSettiong目录方式，创建构建工程(OSX下Unity不支持符号链接，暂时无法使用该功能)<br>
- 构建时对构建列表进行依赖分析，根据依赖关系进行任务分组，确保有依赖关系的构建资源从属同一节点<br>
- 使用子进程方式启动多个Unity进程，并行进行资源构建

# 数据对比
在ssd+i7的pc上进行实验，测试工程单节点进行全量资源构建需要300min，同样测试条件下，修改为5节点并行构建后缩短为24min，效果显著<br>

# 用例
1. 选择菜单"MultiProcessBuild/Profile"进入Profile面板,点击"Create Slaves"创建多个构建工程(OSX不支持，需要整工程复制)<br>
2. 使用"Exampl/Build"进行资源构建
