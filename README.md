# Unity-MultiProcess-BuildPipeline
多进程资源构建方案

# 设计
- Windows借助第三方软件junction，通过符号链接Assets/ProjectSettiong目录方式，创建构建工程<br>
- OSX下由于Unity不支持Assets目录为符号链接，因此采用了为子工程创建真实Assets目录，对主工程Assets根目录所有文件夹、文件进行符号链接的方式代替(-_-!)<br>
- 构建时对构建列表进行依赖分析，根据依赖关系进行任务分组，确保有依赖关系的构建资源从属同一节点<br>
- 使用子进程方式启动多个Unity进程，并行进行资源构建

# 数据对比
在ssd+i7的pc上进行实验，测试工程单节点进行全量资源构建需要300min，同样测试条件下，修改为5节点并行构建后缩短为42min，效果显著<br>

# 用例
1. 选择菜单"MultiProcessBuild/Profile"进入Profile面板,点击"Create Slaves"创建多个构建工程<br>
2. 使用"Exampl/Build"进行资源构建
