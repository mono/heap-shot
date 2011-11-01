NAME = HeapShot.Gui
TARGET = $(NAME).exe
CSPROJ = $(NAME).csproj
XBUILD = xbuild
XBUILD_ARGS = "/property:Configuration=Debug" $(CSPROJ)

bin_SCRIPTS =			\
	$(WRAPPER)

CSFILES = $(wildcard *.cs) $(wildcard */*.cs)

$(TARGET): $(CSFILES) $(CSPROJ)
	$(XBUILD) $(XBUILD_ARGS)

all: $(TARGET) $(WRAPPER)

install-data-local: $(TARGET)
	$(mkinstalldirs) $(DESTDIR)$(pkglibdir)
	$(INSTALL_DATA) $(TARGET) $(DESTDIR)$(pkglibdir)

uninstall-local:
	cd $(DESTDIR)$(pkglibdir) && rm -f $(TARGET)

EXTRA_DIST = 			\
	$(CSFILES)		\
	$(WRAPPER).in

CLEANFILES =			\
	$(WRAPPER)

clean-local:
	rm -f *.dll *.mdb *.exe
	rm -rf obj bin
